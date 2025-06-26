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

public class SimpleHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
}

public class StellarDotnet
{
    // sneed and derived properties
    public string sneed;
    MuxedAccount.KeyTypeEd25519 userAccount => MuxedAccount.FromSecretSeed(sneed);
    public string userAddress => StrKey.EncodeStellarAccountId(userAccount.PublicKey);
    SCVal.ScvAddress userAddressSCVal => new()
    {
        address = new SCAddress.ScAddressTypeAccount()
        {
            accountId = new AccountID(userAccount.XdrPublicKey),
        },
    };

    long latestLedger;
    
    // contract address and derived properties
    public string contractAddress;
    const int maxAttempts = 10;
    
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
    }

    public void SetSneed(string inSneed)
    {
        sneed = inSneed;
    }
    
    public void SetContractId(string inContractAddress)
    {
        contractAddress = inContractAddress;
    }

    public async Task<(GetTransactionResult, SimulateTransactionResult)> CallVoidFunction(string functionName, IScvMapCompatable obj)
    {
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        List<SCVal> argsList = new() { userAddressSCVal };
        if (obj != null)
        {
            SCVal data = obj.ToScvMap();
            argsList.Add(data);
        }
        SCVal[] args = argsList.ToArray();
        (SendTransactionResult sendResult, SimulateTransactionResult simResult) = await InvokeContractFunction(accountEntry, functionName, args);
        if (simResult.Error != null)
        {
            return (null, simResult);
        }
        GetTransactionResult getResult = await WaitForTransaction(sendResult.Hash, 1000);
        if (getResult == null)
        {
            Debug.LogError("CallVoidFunction: timed out or failed to connect");
        }
        else if (getResult.Status != GetTransactionResult_Status.SUCCESS)
        {
            Debug.LogWarning($"CallVoidFunction: status: {getResult.Status}");
        }
        return (getResult, simResult);
    }
    
    public async Task<AccountEntry> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account)
    {
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {EncodedAccountKey(account)},
        });
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
            return entry?.account;
        }
    }
    
    public async Task<LedgerEntry.dataUnion.Trustline> GetAssets(MuxedAccount.KeyTypeEd25519 account)
    {
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {EncodedTrustlineKey(account)},
        });
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.Trustline entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Trustline;
            return entry;
        }
    }
    
    public async Task<NetworkState> ReqNetworkState()
    {
        NetworkState networkState = new(userAddress);
        User? mUser = await ReqUser(userAddress);
        networkState.user = mUser;
        if (mUser?.current_lobby is uint lobbyId)
        {
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = await ReqLobbyStuff(lobbyId);
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
        }
        return networkState;
    }
    
    async Task<User?> ReqUser(AccountAddress key)
    {
        // try to use the stellar rpc client
        LedgerKey ledgerKey = MakeLedgerKey("User", key, ContractDataDurability.PERSISTENT);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {LedgerKeyXdr.EncodeToBase64(ledgerKey)},
        });
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            return null;
        }
        else
        {
            Entries entries = getLedgerEntriesResult.Entries.First();
            if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
            {
                throw new Exception($"ReqUserData on {key} failed because data was not ContractData");
            }
            User user = SCUtility.SCValToNative<User>(data.contractData.val);
            return user;
        }
    }

    async Task<(LobbyInfo?, LobbyParameters?, GameState?)> ReqLobbyStuff(uint key)
    {
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
        });
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
        return tuple;
    }
    
    async Task<(SendTransactionResult, SimulateTransactionResult)> InvokeContractFunction(AccountEntry accountEntry, string functionName, SCVal[] args)
    {
        Transaction invokeContractTransaction = InvokeContractTransaction(functionName, accountEntry, args);
        SimulateTransactionResult simulateTransactionResult = await SimulateTransactionAsync(new SimulateTransactionParams()
        {
            Transaction = EncodeTransaction(invokeContractTransaction),
            ResourceConfig = new()
            {
                // TODO: setup resource config
            }
        });
        if (simulateTransactionResult.Error != null)
        {
            return (null, simulateTransactionResult);
        }
        Transaction assembledTransaction = simulateTransactionResult.ApplyTo(invokeContractTransaction);
        string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
        SendTransactionResult sendTransactionResult = await SendTransactionAsync(new SendTransactionParams()
        {
            Transaction = encodedSignedTransaction,
        });
        return (sendTransactionResult, simulateTransactionResult);
    }
    
    Transaction InvokeContractTransaction(string functionName, AccountEntry accountEntry, SCVal[] args)
    {
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
        return new Transaction()
        {
            sourceAccount = userAccount,
            fee = 100, // TODO: make this configurable
            memo = new Memo.MemoNone(),
            seqNum = accountEntry.seqNum.Increment(), // TODO: sometimes we might not want to increment here
            cond = new Preconditions.PrecondNone(),
            ext = new Transaction.extUnion.case_0(),
            operations = new[] { operation },
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

    async Task<GetTransactionResult> WaitForTransaction(string txHash, int delayMS)
    {
        
        int attempts = 0;
        await AsyncDelay.Delay(delayMS);
        while (attempts < maxAttempts)
        {
            attempts++;
            GetTransactionResult completion = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash
            });
            switch (completion.Status)
            {
                case GetTransactionResult_Status.FAILED:
                    Debug.Log("WaitForTransaction: FAILED");
                    return completion;
                case GetTransactionResult_Status.NOT_FOUND:
                    await AsyncDelay.Delay(delayMS);
                    continue;
                case GetTransactionResult_Status.SUCCESS:
                    Debug.Log("WaitForTransaction: SUCCESS");
                    return completion;
            }
        }
        Debug.Log("WaitForTransaction: timed out");
        return null;
    }
    
    
    // variant of StellarRPCClient.SimulateTransactionAsync()
    async Task<SimulateTransactionResult> SimulateTransactionAsync(SimulateTransactionParams parameters = null)
    {
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "simulateTransaction",
            Params = parameters,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JObject jsonObject = JObject.Parse(content);
        // NOTE: Remove "stateChanges" entirely to avoid deserialization issues
        JObject resultObj = (JObject)jsonObject["result"];
        resultObj.Remove("stateChanges");
        try
        {
            JsonRpcResponse<SimulateTransactionResult> rpcResponse = jsonObject.ToObject<JsonRpcResponse<SimulateTransactionResult>>();
            SimulateTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
            return transactionResult;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }

    }
    
    // variant of StellarRPCClient.SendTransactionAsync()
    async Task<SendTransactionResult> SendTransactionAsync(SendTransactionParams parameters = null)
    {
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "sendTransaction",
            Params = parameters,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<SendTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<SendTransactionResult>>(content, this.jsonSettings);
        SendTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        return transactionResult;
    }
    
    // variant of StellarRPCClient.GetLedgerEntriesAsync()
    async Task<GetLedgerEntriesResult> GetLedgerEntriesAsync(GetLedgerEntriesParams parameters = null)
    {
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getLedgerEntries",
            Params = parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject(request, this.jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<GetLedgerEntriesResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLedgerEntriesResult>>(content, this.jsonSettings);
        GetLedgerEntriesResult ledgerEntriesAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        latestLedger = ledgerEntriesAsync.LatestLedger;
        return ledgerEntriesAsync;
    }
    
    // variant of StellarRPCClient.GetTransactionAsync()
    async Task<GetTransactionResult> GetTransactionAsync(GetTransactionParams parameters = null)
    {
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getTransaction",
            Params = parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject(request, this.jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<GetTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetTransactionResult>>(content, this.jsonSettings);
        GetTransactionResult transactionAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        latestLedger = transactionAsync.LatestLedger;
        return transactionAsync;
    }
    
    async Task<string> SendJsonRequest(string json)
    {
        UnityWebRequest request = new(networkUri, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Debug.Log($"SendJsonRequest: request: {json}");
        await request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"SendJsonRequest: error: {request.error}");
        }
        else
        {
            Debug.Log($"SendJsonRequest: response: {request.downloadHandler.text}");
        }
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