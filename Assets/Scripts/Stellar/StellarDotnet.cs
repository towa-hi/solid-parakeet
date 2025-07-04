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

public class StellarDotnet
{
    // sneed and derived properties
    public string sneed;
    MuxedAccount.KeyTypeEd25519 cachedUserAccount;
    string cachedUserAddress;
    SCVal.ScvAddress cachedUserAddresSCVal;
    
    MuxedAccount.KeyTypeEd25519 userAccount
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
    
    [ThreadStatic]
    private static TimingTracker currentTracker;
    
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

    public async Task<(GetTransactionResult, SimulateTransactionResult)> CallVoidFunction(string functionName, IScvMapCompatable obj)
    {
        currentTracker = new TimingTracker();
        currentTracker.StartOperation($"CallVoidFunction({functionName})");
        
        currentTracker.StartOperation("ReqAccountEntry");
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        currentTracker.EndOperation();
        
        List<SCVal> argsList = new() { userAddressSCVal };
        if (obj != null)
        {
            SCVal data = obj.ToScvMap();
            argsList.Add(data);
        }
        SCVal[] args = argsList.ToArray();
        
        currentTracker.StartOperation($"InvokeContractFunction({functionName})");
        (SendTransactionResult sendResult, SimulateTransactionResult simResult) = await InvokeContractFunction(accountEntry, functionName, args);
        currentTracker.EndOperation();
        
        if (simResult.Error != null)
        {
            currentTracker.EndOperation();
            Debug.Log(currentTracker.GetReport());
            currentTracker = null;
            return (null, simResult);
        }
        
        currentTracker.StartOperation("WaitForTransaction");
        GetTransactionResult getResult = await WaitForTransaction(sendResult.Hash, 1000);
        currentTracker.EndOperation();
        
        if (getResult == null)
        {
            Debug.LogError("CallVoidFunction: timed out or failed to connect");
        }
        else if (getResult.Status != GetTransactionResult_Status.SUCCESS)
        {
            Debug.LogWarning($"CallVoidFunction: status: {getResult.Status}");
        }
        
        currentTracker.EndOperation();
        Debug.Log(currentTracker.GetReport());
        currentTracker = null;
        
        return (getResult, simResult);
    }
    
    public async Task<AccountEntry> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account)
    {
        currentTracker?.StartOperation("EncodedAccountKey");
        string encodedKey = EncodedAccountKey(account);
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("GetLedgerEntriesAsync");
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        });
        currentTracker?.EndOperation();
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
        bool isTopLevel = currentTracker == null;
        if (isTopLevel)
        {
            currentTracker = new TimingTracker();
            currentTracker.StartOperation("GetAssets");
        }
        
        currentTracker?.StartOperation("EncodedTrustlineKey");
        string encodedKey = EncodedTrustlineKey(account);
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("GetLedgerEntriesAsync");
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        });
        currentTracker?.EndOperation();
        
        if (isTopLevel)
        {
            currentTracker.EndOperation();
            Debug.Log(currentTracker.GetReport());
            currentTracker = null;
        }
        
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
        currentTracker = new TimingTracker();
        currentTracker.StartOperation("ReqNetworkState");
        
        NetworkState networkState = new(userAddress);
        
        currentTracker.StartOperation("ReqUser");
        User? mUser = await ReqUser(userAddress);
        currentTracker.EndOperation();
        
        networkState.user = mUser;
        if (mUser is User user && user.current_lobby != 0)
        {
            currentTracker.StartOperation("ReqLobbyStuff");
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = await ReqLobbyStuff(user.current_lobby);
            currentTracker.EndOperation();
            
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
        }
        
        currentTracker.EndOperation();
        Debug.Log(currentTracker.GetReport());
        currentTracker = null;
        
        return networkState;
    }
    
    async Task<User?> ReqUser(AccountAddress key)
    {
        // try to use the stellar rpc client
        currentTracker?.StartOperation("MakeLedgerKey");
        LedgerKey ledgerKey = MakeLedgerKey("User", key, ContractDataDurability.PERSISTENT);
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("EncodeToBase64");
        string encodedKey = LedgerKeyXdr.EncodeToBase64(ledgerKey);
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("GetLedgerEntriesAsync");
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        });
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation("GetLedgerEntriesAsync (3 keys)");
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                lobbyInfoKey,
                lobbyParametersKey,
                gameStateKey,
            },
        });
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation("SimulateTransactionAsync");
        SimulateTransactionResult simulateTransactionResult = await SimulateTransactionAsync(new SimulateTransactionParams()
        {
            Transaction = EncodeTransaction(invokeContractTransaction),
            ResourceConfig = new()
            {
                // TODO: setup resource config
            }
        });
        currentTracker?.EndOperation();
        
        if (simulateTransactionResult.Error != null)
        {
            return (null, simulateTransactionResult);
        }
        Transaction assembledTransaction = simulateTransactionResult.ApplyTo(invokeContractTransaction);
        string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
        
        currentTracker?.StartOperation("SendTransactionAsync");
        SendTransactionResult sendTransactionResult = await SendTransactionAsync(new SendTransactionParams()
        {
            Transaction = encodedSignedTransaction,
        });
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation($"Initial delay ({delayMS}ms)");
        await AsyncDelay.Delay(delayMS);
        currentTracker?.EndOperation();
        
        while (attempts < maxAttempts)
        {
            attempts++;
            
            currentTracker?.StartOperation($"GetTransactionAsync (attempt {attempts})");
            GetTransactionResult completion = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash
            });
            currentTracker?.EndOperation();
            
            switch (completion.Status)
            {
                case GetTransactionResult_Status.FAILED:
                    Debug.Log("WaitForTransaction: FAILED");
                    return completion;
                case GetTransactionResult_Status.NOT_FOUND:
                    currentTracker?.StartOperation($"Retry delay ({delayMS}ms)");
                    await AsyncDelay.Delay(delayMS);
                    currentTracker?.EndOperation();
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
        
        currentTracker?.StartOperation("SendJsonRequest");
        string content = await SendJsonRequest(requestJson);
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation("SendJsonRequest");
        string content = await SendJsonRequest(requestJson);
        currentTracker?.EndOperation();
        
        JsonRpcResponse<SendTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<SendTransactionResult>>(content, jsonSettings);
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
        currentTracker?.StartOperation("JSON Serialize Request");
        long serializeStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        long serializeEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("SendJsonRequest");
        string content = await SendJsonRequest(requestJson);
        currentTracker?.EndOperation();
        
        currentTracker?.StartOperation("JSON Deserialize Response");
        JsonRpcResponse<GetLedgerEntriesResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLedgerEntriesResult>>(content, this.jsonSettings);
        GetLedgerEntriesResult ledgerEntriesAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation("SendJsonRequest");
        string content = await SendJsonRequest(requestJson);
        currentTracker?.EndOperation();
        
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
        
        currentTracker?.StartOperation("SendWebRequest");
        await request.SendWebRequest();
        currentTracker?.EndOperation();
        
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


public class TimingNode
{
    public string Name { get; set; }
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public long ElapsedMs => EndTime - StartTime;
    public List<TimingNode> Children { get; set; } = new List<TimingNode>();
    public TimingNode Parent { get; set; }
}

public class TimingTracker
{
    private TimingNode root;
    private TimingNode current;
    
    public void StartOperation(string name)
    {
        var node = new TimingNode 
        { 
            Name = name, 
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Parent = current
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
        sb.AppendLine($"\n=== Performance Report for {root.Name} ===");
        sb.AppendLine($"Total time: {root.ElapsedMs}ms");
        sb.AppendLine("\nBreakdown:");
        
        PrintNode(sb, root, "", true);
        
        return sb.ToString();
    }
    
    private void PrintNode(StringBuilder sb, TimingNode node, string indent, bool isLast)
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
