using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

public class StellarDotnet
{
    // sneed and derived properties
    public string sneed;
    MuxedAccount.KeyTypeEd25519 cachedUserAccount;
    string cachedUserAddress;
    SCVal.ScvAddress cachedUserAddresSCVal;
    
    public MuxedAccount.KeyTypeEd25519 userAccount
    {
        get
        {
            if (cachedUserAccount == null && !string.IsNullOrEmpty(sneed))
            {
                cachedUserAccount = MuxedAccount.FromSecretSeed(sneed);
            }
            return cachedUserAccount;
        }
    }
    
    public string userAddress
    {
        get
        {
            if (cachedUserAddress == null && userAccount != null)
            {
                cachedUserAddress = StrKey.EncodeStellarAccountId(userAccount.PublicKey);
            }
            return cachedUserAddress;
        }
    }
    
    SCVal.ScvAddress userAddressSCVal
    {
        get
        {
            if (cachedUserAddresSCVal == null && userAccount != null)
            {
                cachedUserAddresSCVal = new SCVal.ScvAddress()
                {
                    address = new SCAddress.ScAddressTypeAccount()
                    {
                        accountId = new AccountID(userAccount.XdrPublicKey),
                    },
                };
            }
            return cachedUserAddresSCVal;
        }
    }

    long latestLedger;
    
    // contract address and derived properties
    public string contractAddress;
    const int maxAttempts = 30;
    
    Uri networkUri;
    // ReSharper disable once InconsistentNaming
    JsonSerializerSettings jsonSettings = new()
    {
        ContractResolver =  new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };
    
    public StellarDotnet(string inSecretSneed, string inContractId)
    {
        networkUri = new Uri("https://soroban-testnet.stellar.org");
        Network.UseTestNetwork();
        SetSneed(inSecretSneed);
        SetContractId(inContractId);
        
        // Warm up JSON.NET to avoid first-call initialization delay
        WarmUpJsonSerializer();
    }
    
    private void WarmUpJsonSerializer()
    {
        try
        {
            // Create a dummy request similar to what we'll actually serialize
            var dummyRequest = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Method = "getLedgerEntries",
                Params = new GetLedgerEntriesParams
                {
                    Keys = new[] { "dummy" }
                },
                Id = 1
            };
            
            // Perform serialization to warm up JSON.NET
            long warmupStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string warmupJson = JsonConvert.SerializeObject(dummyRequest, jsonSettings);
            long warmupEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Debug.Log($"JSON.NET warmup completed in {warmupEnd - warmupStart}ms");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"JSON.NET warmup failed: {e.Message}");
        }
    }

    public void SetSneed(string inSneed)
    {
        sneed = inSneed;
        // Clear cached values when sneed changes
        cachedUserAccount = null;
        cachedUserAddress = null;
        cachedUserAddresSCVal = null;
    }
    
    public void SetContractId(string inContractAddress)
    {
        contractAddress = inContractAddress;
    }

    async Task<bool> GetEvents(LobbyId lobbyId, string symbol, TimingTracker tracker = null)
    {
        tracker?.StartOperation("GetEvents");
        string lobbyIdTopic = SCValXdr.EncodeToBase64(SCUtility.NativeToSCVal(lobbyId));
        Filters filter = new()
        {
            Type = "contract",
            ContractIds = new string[] {contractAddress},
            Topics = new string[][] {new string[] {lobbyIdTopic, "*"}},
            AdditionalProperties = null,
        };
        GetEventsResult result = await GetEventsAsync(new GetEventsParams
        {
            StartLedger = latestLedger - 1000,
            Filters = new Filters[] { filter },
            Pagination = null,
            AdditionalProperties = null,
        });
        foreach (Events ev in result.Events)
        {
            foreach (string topic in ev.Topic)
            {
                SCVal topicSCVal = SCValXdr.DecodeFromBase64(topic);
                Debug.Log(JsonConvert.SerializeObject(topicSCVal));
            }
            SCVal eventSCVal = SCValXdr.DecodeFromBase64(ev.Value);
            Debug.Log(JsonConvert.SerializeObject(eventSCVal));
        }
        tracker?.EndOperation();
        return true;
    }

    // public async Task<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> CallVoidFunctionWithTwoParameters(string functionName, IScvMapCompatable request1, IScvMapCompatable request2, TimingTracker tracker = null)
    // {
    //     tracker?.StartOperation($"CallVoidFunctionWithTwoParameters {functionName}");
    //     AccountEntry accountEntry = await ReqAccountEntry(userAccount, tracker);
    //     List<SCVal> argsList = new()
    //     {
    //         userAddressSCVal,
    //         request1.ToScvMap(),
    //         request2.ToScvMap(),
    //     };
    //     Transaction invokeContractTransaction = BuildInvokeContractTransaction(accountEntry, functionName, argsList.ToArray(), true);
    //     SimulateTransactionResult simResult = await SimulateTransactionAsync(
    //         new SimulateTransactionParams()
    //         {
    //             Transaction = EncodeTransaction(invokeContractTransaction),
    //             ResourceConfig = new()
    //             {
    //                 
    //                 InstructionLeeway = 10000000,
    //             },
    //         }, tracker);
    //     if (simResult is not {Error: null})
    //     {
    //         tracker?.EndOperation();
    //         return (simResult, null, null);
    //     }
    //     Transaction assembledTransaction = simResult.ApplyTo(invokeContractTransaction);
    //     string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
    //     SendTransactionResult sendResult = await SendTransactionAsync(new SendTransactionParams()
    //     {
    //         Transaction = encodedSignedTransaction,
    //     }, tracker);
    //     if (sendResult.Status == SendTransactionResult_Status.ERROR)
    //     {
    //         tracker?.EndOperation();
    //         return (simResult, sendResult, null);
    //     }
    //     GetTransactionResult getResult = await WaitForGetTransactionResult(sendResult.Hash, 1000, tracker);
    //     tracker?.EndOperation();
    //     return (simResult, sendResult, getResult);
    // }

    public async Task<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> CallContractFunction(string functionName, IScvMapCompatable arg, TimingTracker tracker = null)
    {
        return await CallContractFunction(functionName, new IScvMapCompatable[] {arg}, tracker);
    }
    
    public async Task<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> CallContractFunction(string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"CallContractFunction {functionName}");
        (Transaction transaction, SimulateTransactionResult simResult) = await SimulateContractFunction(functionName, args, tracker);
        if (simResult is not {Error: null})
        {
            tracker?.EndOperation();
            return (simResult, null, null);
        }
        Transaction assembledTransaction = simResult.ApplyTo(transaction);
        // double all fees just to make sure the transaction actually goes through
        uint fee = assembledTransaction.fee.InnerValue;
        assembledTransaction.fee = fee * 2;
        if (assembledTransaction.ext is Transaction.extUnion.case_1 v1)
        {
            v1.sorobanData.resources.readBytes *= 2;
            v1.sorobanData.resources.writeBytes *= 2;
            v1.sorobanData.resourceFee *= 2;
        }
        string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
        SendTransactionResult sendResult = await SendTransactionAsync(new SendTransactionParams()
        {
            Transaction = encodedSignedTransaction,
        }, tracker);
        if (sendResult is not { ErrorResult: null })
        {
            tracker?.EndOperation();
            return (simResult, sendResult, null);
        }
        GetTransactionResult getResult = await WaitForGetTransactionResult(sendResult.Hash, 200, tracker);
        tracker?.EndOperation();
        return (simResult, sendResult, getResult);
    }
    
    public async Task<(Transaction, SimulateTransactionResult)> SimulateContractFunction(string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"SimulateContractFunction {functionName}");
        AccountEntry accountEntry = await ReqAccountEntry(userAccount, tracker);
        List<SCVal> argsList = new() { userAddressSCVal };
        foreach (IScvMapCompatable arg in args)
        {
            argsList.Add(arg.ToScvMap());
        }
        Transaction invokeContractTransaction = BuildInvokeContractTransaction(accountEntry, functionName, argsList.ToArray(), true);
        SimulateTransactionResult simulateTransactionResult = await SimulateTransactionAsync(
            new SimulateTransactionParams()
            {
                Transaction = EncodeTransaction(invokeContractTransaction),
                ResourceConfig = new()
                {
                    InstructionLeeway = 10000000,
                },
            },
            tracker);
        tracker?.EndOperation();
        return (invokeContractTransaction, simulateTransactionResult);
    }
    
    public async Task<AccountEntry> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqAccountEntry");
        string encodedKey = EncodedAccountKey(account);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
            tracker?.EndOperation();
            return entry?.account;
        }
    }
    
    public async Task<LedgerEntry.dataUnion.Trustline> GetAssets(MuxedAccount.KeyTypeEd25519 account, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetAssets");
        string encodedKey = EncodedTrustlineKey(account);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.Trustline entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Trustline;
            tracker?.EndOperation();
            return entry;
        }
    }
    
    public async Task<NetworkState> ReqNetworkState(TimingTracker tracker = null)
    {
        tracker?.StartOperation("ReqNetworkState");
        
        NetworkState networkState = new(userAddress);
        
        User? mUser = await ReqUser(userAddress, tracker);
        networkState.user = mUser;
        if (mUser is User user && user.current_lobby != 0)
        {
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = await ReqLobbyStuff(user.current_lobby, tracker);
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
            if (networkState.lobbyInfo is LobbyInfo lobbyInfo)
            {
                // await GetEvents(lobbyInfo.index, "WEWLAD", tracker);
            }
        }
        tracker?.EndOperation();
        return networkState;
    }
    
    async Task<User?> ReqUser(AccountAddress key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqUser");
        // try to use the stellar rpc client
        LedgerKey ledgerKey = MakeLedgerKey("User", key, ContractDataDurability.PERSISTENT);
        string encodedKey = LedgerKeyXdr.EncodeToBase64(ledgerKey);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return null;
        }
        Entries entries = getLedgerEntriesResult.Entries.First();
        if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
        {
            throw new Exception($"ReqUserData on {key} failed because data was not ContractData");
        }
        User user = SCUtility.SCValToNative<User>(data.contractData.val);
        tracker?.EndOperation();
        return user;
    }

    public async Task<LobbyInfo?> ReqLobbyInfo(uint key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqLobbyStuff");
        string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyInfo", key, ContractDataDurability.TEMPORARY));
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                lobbyInfoKey,
            },
        }, tracker);
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return null;
        }
        Entries entries = getLedgerEntriesResult.Entries.First();
        if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
        {
            throw new Exception($"ReqUserData on {key} failed because data was not ContractData");
        }
        LobbyInfo lobbyInfo = SCUtility.SCValToNative<LobbyInfo>(data.contractData.val);
        tracker?.EndOperation();
        return lobbyInfo;
    }
    
    async Task<(LobbyInfo?, LobbyParameters?, GameState?)> ReqLobbyStuff(uint key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqLobbyStuff");
        string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyInfo", key, ContractDataDurability.TEMPORARY));
        string lobbyParametersKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyParameters", key, ContractDataDurability.TEMPORARY));
        string gameStateKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("GameState", key, ContractDataDurability.TEMPORARY));
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                lobbyInfoKey,
                lobbyParametersKey,
                gameStateKey,
            },
        }, tracker);
        (LobbyInfo?, LobbyParameters?, GameState?) tuple = (null, null, null);
        foreach (Entries entry in getLedgerEntriesResult.Entries)
        {
            if (entry.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
            {
                throw new Exception($"ReqLobbyStuff on {key} failed because data was not ContractData");
            }
            if (entry.Key == lobbyInfoKey)
            {
                LobbyInfo lobbyInfo = SCUtility.SCValToNative<LobbyInfo>(data.contractData.val);
                lobbyInfo.liveUntilLedgerSeq = entry.LiveUntilLedgerSeq;
                tuple.Item1 = lobbyInfo;
            }
            else if (entry.Key == lobbyParametersKey)
            {
                LobbyParameters lobbyParameters = SCUtility.SCValToNative<LobbyParameters>(data.contractData.val);
                lobbyParameters.liveUntilLedgerSeq = entry.LiveUntilLedgerSeq;
                tuple.Item2 = lobbyParameters;
            }
            else if (entry.Key == gameStateKey)
            {
                GameState gameState = SCUtility.SCValToNative<GameState>(data.contractData.val);
                gameState.liveUntilLedgerSeq = entry.LiveUntilLedgerSeq;
                tuple.Item3 = gameState;
            }
        }
        tracker?.EndOperation();
        return tuple;
    }
    
    Transaction BuildInvokeContractTransaction(AccountEntry accountEntry, string functionName, SCVal[] args, bool increment)
    {
        List<Operation> operations = new();
        Operation operation = new()
        {
            sourceAccount = userAccount,
            body = new Operation.bodyUnion.InvokeHostFunction()
            {
                invokeHostFunctionOp = new InvokeHostFunctionOp()
                {
                    auth = Array.Empty<SorobanAuthorizationEntry>(),
                    hostFunction = new HostFunction.HostFunctionTypeInvokeContract()
                    {
                        invokeContract = new InvokeContractArgs()
                        {
                            contractAddress = new SCAddress.ScAddressTypeContract()
                            {
                                contractId = new Hash(StrKey.DecodeContractId(contractAddress)),
                            },
                            functionName = new SCSymbol(functionName),
                            args = args,
                        },
                    },
                },
            },
        };
        if (increment)
        {
            accountEntry.seqNum.Increment();
        }
        operations.Add(operation);
        return new Transaction()
        {
            sourceAccount = userAccount,
            fee = 100000, // TODO: make this configurable
            memo = new Memo.MemoNone(),
            seqNum = accountEntry.seqNum, 
            cond = new Preconditions.PrecondNone(),
            ext = new Transaction.extUnion.case_0(),
            operations = operations.ToArray(),
        };
    }

    string EncodeTransaction(Transaction transaction)
    {
        TransactionEnvelope.EnvelopeTypeTx envelope = new()
        {
            v1 = new TransactionV1Envelope()
            {
                tx = transaction,
                signatures = Array.Empty<DecoratedSignature>(),
            },
        };
        return TransactionEnvelopeXdr.EncodeToBase64(envelope);
    }

    string SignAndEncodeTransaction(Transaction transaction)
    {
        DecoratedSignature signature = transaction.Sign(userAccount);
        TransactionEnvelope.EnvelopeTypeTx envelope = new()
        {
            v1 = new TransactionV1Envelope()
            {
                tx = transaction,
                signatures = new[] { signature },
            },
        };
        return TransactionEnvelopeXdr.EncodeToBase64(envelope);
    }

    async Task<GetTransactionResult> WaitForGetTransactionResult(string txHash, int delayMS, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"WaitForGetTransactionResult");
        int attempts = 0;
        
        tracker?.StartOperation($"Initial delay ({delayMS}ms)");
        await AsyncDelay.Delay(delayMS);
        tracker?.EndOperation();
        
        while (attempts < maxAttempts)
        {
            attempts++;
            //delayMS *= 2;
            tracker?.StartOperation($"GetTransactionAsync (attempt {attempts})");
            GetTransactionResult completion = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash,
            });
            tracker?.EndOperation();
            switch (completion.Status)
            {
                case GetTransactionResult_Status.FAILED:
                    Debug.Log("WaitForTransaction: FAILED");
                    tracker?.EndOperation();
                    return completion;
                case GetTransactionResult_Status.NOT_FOUND:
                    tracker?.StartOperation($"Retry delay ({delayMS}ms)");
                    await AsyncDelay.Delay(delayMS);
                    tracker?.EndOperation();
                    continue;
                case GetTransactionResult_Status.SUCCESS:
                    Debug.Log("WaitForTransaction: SUCCESS");
                    tracker?.EndOperation();
                    return completion;
            }
        }
        Debug.Log("WaitForTransaction: timed out");
        tracker?.EndOperation();
        return null;
        
    }
    
    
    // variant of StellarRPCClient.SimulateTransactionAsync()
    async Task<SimulateTransactionResult> SimulateTransactionAsync(SimulateTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation("SimulateTransactionAsync");
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "simulateTransaction",
            Params = parameters,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson, tracker);
        JObject jsonObject = JObject.Parse(content);
        try
        {
            JsonRpcResponse<SimulateTransactionResult> rpcResponse = jsonObject.ToObject<JsonRpcResponse<SimulateTransactionResult>>();
            SimulateTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
            tracker?.EndOperation();
            return transactionResult;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
        
    }
    
    // variant of StellarRPCClient.SendTransactionAsync()
    async Task<SendTransactionResult> SendTransactionAsync(SendTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"SendTransactionAsync");
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "sendTransaction",
            Params = parameters,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson, tracker);
        JsonRpcResponse<SendTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<SendTransactionResult>>(content, jsonSettings);
        SendTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        tracker?.EndOperation();
        return transactionResult;
    }


    async Task<GetEventsResult> GetEventsAsync(GetEventsParams parameters, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetEventsAsync");
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getEvents",
            Params = parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson, tracker);
        JsonRpcResponse<GetEventsResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetEventsResult>>(content, jsonSettings);
        latestLedger = rpcResponse.Result.LatestLedger;
        tracker?.EndOperation();
        return rpcResponse.Result;
    }
    
    // variant of StellarRPCClient.GetLedgerEntriesAsync()
    async Task<GetLedgerEntriesResult> GetLedgerEntriesAsync(GetLedgerEntriesParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetLedgerEntriesAsync");
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getLedgerEntries",
            Params = parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson, tracker);
        JsonRpcResponse<GetLedgerEntriesResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLedgerEntriesResult>>(content, this.jsonSettings);
        GetLedgerEntriesResult ledgerEntriesAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        latestLedger = ledgerEntriesAsync.LatestLedger;
        tracker?.EndOperation();
        return ledgerEntriesAsync;
    }
    
    // variant of StellarRPCClient.GetTransactionAsync()
    async Task<GetTransactionResult> GetTransactionAsync(GetTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetTransactionAsync");
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getTransaction",
            Params = parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson, tracker);
        JsonRpcResponse<GetTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetTransactionResult>>(content, this.jsonSettings);
        GetTransactionResult transactionAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        latestLedger = transactionAsync.LatestLedger;
        tracker?.EndOperation();
        return transactionAsync;
    }
    
    async Task<string> SendJsonRequest(string json, TimingTracker tracker = null)
    {
        tracker?.StartOperation("SendJsonRequest");
        UnityWebRequest request = new(networkUri, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Debug.Log($"SendJsonRequest: request: {json}");
        
        tracker?.StartOperation("SendWebRequest");
        await request.SendWebRequest();
        tracker?.EndOperation();
        
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"SendJsonRequest: error: {request.error}");
        }
        else
        {
            Debug.Log($"SendJsonRequest: response: {request.downloadHandler.text}");
        }
        tracker?.EndOperation();
        return request.downloadHandler.text;
    }
    
    LedgerKey MakeLedgerKey(string sym, object key, ContractDataDurability durability)
    {
        SCVal scKey = SCUtility.NativeToSCVal(key);
        SCVal.ScvVec enumKey = new()
        {
            vec = new SCVec(new[]
            {
                new SCVal.ScvSymbol
                {
                    sym = sym,
                },
                scKey,
            }),
        };
        return new LedgerKey.ContractData
        {
            contractData = new LedgerKey.contractDataStruct
            {
                contract = new SCAddress.ScAddressTypeContract
                {
                    contractId = new Hash(StrKey.DecodeContractId(contractAddress)),
                },
                key = enumKey,
                durability = durability,
            },
        };
    }

    static string EncodedAccountKey(MuxedAccount.KeyTypeEd25519 account)
    {
        return LedgerKeyXdr.EncodeToBase64(new LedgerKey.Account()
        {
            account = new LedgerKey.accountStruct()
            {
                accountID = account.XdrPublicKey,
            },
        });
    }

    string EncodedTrustlineKey(MuxedAccount.KeyTypeEd25519 account)
    {
        string code = "SCRY";
        string issuerAccountId = "GAAPZLAZJ5SL4IL63WHFWRUWPK2UV4SREUOWM2DZTTQR7FJPFQAHDSNG";
        AccountID issuerAccount = MuxedAccount.FromAccountId(issuerAccountId).XdrPublicKey;
        byte[] codeBytes = Encoding.ASCII.GetBytes(code);
        return LedgerKeyXdr.EncodeToBase64(new LedgerKey.Trustline
        {
            trustLine = new LedgerKey.trustLineStruct
            {
                accountID = account.XdrPublicKey,
                asset = new TrustLineAsset.AssetTypeCreditAlphanum4
                {
                    alphaNum4 = new AlphaNum4
                    {
                        assetCode = new AssetCode4
                        {
                            InnerValue = codeBytes,
                        },
                        issuer = issuerAccount,
                    },
                },
            },
        });
    }
}

public static class AsyncDelay
{
    public static Task Delay(int millisecondsDelay)
    {
        // Use a coroutine-based delay in WebGL
        return WaitForSecondsAsync(millisecondsDelay / 1000f);
    }

    static Task WaitForSecondsAsync(float seconds)
    {
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        CoroutineRunner.instance.StartCoroutine(WaitForSecondsCoroutine(seconds, tcs));
        return tcs.Task;
    }

    static IEnumerator WaitForSecondsCoroutine(float seconds, TaskCompletionSource<bool> tcs)
    {
        yield return new WaitForSeconds(seconds);
        tcs.SetResult(true);
    }
}

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner ins;
    public static CoroutineRunner instance
    {
        get
        {
            if (!ins)
            {
                // Create a new GameObject to attach the runner
                GameObject go = new("CoroutineRunner");
                ins = go.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(go);
            }
            return ins;
        }
    }
}


public class TimingNode
{
    public string Name { get; init; }
    public long StartTime { get; init; }
    public long EndTime { get; set; }
    public long ElapsedMs => EndTime - StartTime;
    public List<TimingNode> Children { get; } = new();
    public TimingNode Parent { get; init; }
}

public class TimingTracker
{
    TimingNode root;
    TimingNode current;
    
    public void StartOperation(string name)
    {
        TimingNode node = new()
        { 
            Name = name, 
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Parent = current,
        };
        
        if (root == null)
        {
            root = node;
            current = node;
        }
        else
        {
            current.Children.Add(node);
            current = node;
        }
    }
    
    public void EndOperation()
    {
        if (current != null)
        {
            current.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            current = current.Parent;
        }
    }
    
    public string GetReport()
    {
        if (root == null) return "No timing data";
        
        var sb = new StringBuilder();
        sb.AppendLine($"\n=== Performance Report for {root.Name} Total time: {root.ElapsedMs}ms ===");
        sb.AppendLine("\nBreakdown:");
        
        PrintNode(sb, root, "", true);
        
        return sb.ToString();
    }
    
    void PrintNode(StringBuilder sb, TimingNode node, string indent, bool isLast)
    {
        if (node != root)
        {
            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");
            sb.AppendLine($"{node.Name}: {node.ElapsedMs}ms");
        }
        
        for (int i = 0; i < node.Children.Count; i++)
        {
            string childIndent = indent + (node == root ? "" : (isLast ? "    " : "│   "));
            PrintNode(sb, node.Children[i], childIndent, i == node.Children.Count - 1);
        }
    }
}
