using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Stellar;
using System.Reflection;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Networking;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

public static class StellarDotnet
{
    // sneed and derived properties
    public static string sneed;
    static MuxedAccount.KeyTypeEd25519 cachedUserAccount;
    static string cachedUserAddress;
    static SCVal.ScvAddress cachedUserAddresSCVal;

    static bool isWallet;
    
    public static MuxedAccount.KeyTypeEd25519 userAccount
    {
        get
        {
            if (isWallet)
            {
                return MuxedAccount.FromPublicKey(StrKey.DecodeStellarAccountId(WalletManager.address));
            }
            if (cachedUserAccount == null && !string.IsNullOrEmpty(sneed))
            {
                cachedUserAccount = MuxedAccount.FromSecretSeed(sneed);
            }
            return cachedUserAccount;
        }
    }
    
    public static string userAddress
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
    
    static SCVal.ScvAddress userAddressSCVal
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

    static long latestLedger;
    
    static void DebugLog(string message)
    {
        if (ResourceRoot.DefaultSettings.networkLogging)
        {
            Debug.Log(message);
        }
    }
    // contract address and derived properties
    public static string contractAddress;
    const int maxAttempts = 30;
    
    static Uri networkUri;
    // ReSharper disable once InconsistentNaming
    static readonly JsonSerializerSettings jsonSettings = new()
    {
        ContractResolver =  new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy
            {
                ProcessDictionaryKeys = true,
                OverrideSpecifiedNames = false,
            }
        },
        NullValueHandling = NullValueHandling.Ignore,
    };

    // Removed Soroban XDR normalization helpers (obsolete)
    
    public static Result<bool> Initialize(bool isTestnet, bool inIsWallet, string inSecretSneed, string inContractId)
    {
        // Validate inputs early to avoid silent failures later
        if (string.IsNullOrEmpty(inSecretSneed) || !StrKey.IsValidEd25519SecretSeed(inSecretSneed))
        {
            return Result<bool>.Err(StatusCode.OTHER_ERROR, "Initialize: invalid or missing secret seed (sneed)");
        }
        if (string.IsNullOrEmpty(inContractId) || !StrKey.IsValidContractId(inContractId))
        {
            return Result<bool>.Err(StatusCode.OTHER_ERROR, "Initialize: invalid or missing contract id");
        }
        if (isTestnet)
        {
            networkUri = new Uri("https://soroban-testnet.stellar.org");
            Network.UseTestNetwork();
        }
        else
        {
            networkUri = new Uri("https://soroban-mainnet.stellar.org");
            Network.UsePublicNetwork();
        }
        
        isWallet = inIsWallet;
        SetSneed(inSecretSneed);
        SetContractId(inContractId);
        
        // Warm up JSON.NET to avoid first-call initialization delay
        WarmUpJsonSerializer();
        return Result<bool>.Ok(true);
    }
    
    static void WarmUpJsonSerializer()
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
            
            DebugLog($"JSON.NET warmup completed in {warmupEnd - warmupStart}ms");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"JSON.NET warmup failed: {e.Message}");
        }
    }

    public static void SetSneed(string inSneed)
    {
        sneed = inSneed;
        // Clear cached values when sneed changes
        cachedUserAccount = null;
        cachedUserAddress = null;
        cachedUserAddresSCVal = null;
    }
    
    public static void SetContractId(string inContractAddress)
    {
        contractAddress = inContractAddress;
    }

    public static async Task<Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>> CallContractFunction(string functionName, IScvMapCompatable arg, TimingTracker tracker = null)
    {
        return await CallContractFunction(functionName, new IScvMapCompatable[] {arg}, tracker);
    }
    
    public static async Task<Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>> CallContractFunction(string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"CallContractFunction {functionName}");
        var simResult = await SimulateContractFunction(functionName, args, tracker);
        if (simResult.IsError)
        {
            tracker?.EndOperation();
            return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(simResult);
        }
        (Transaction transaction, SimulateTransactionResult sim) = simResult.Value;
        if (sim is not {Error: null})
        {
            tracker?.EndOperation();
            StatusCode code = HasContractError(sim) ? StatusCode.CONTRACT_ERROR : StatusCode.SIMULATION_FAILED;
            return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(code, (sim, null, null),$"CallContractFunction {functionName} failed because the simulation result was not successful");
        }
        Transaction assembledTransaction = sim.ApplyTo(transaction);
        string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
        var sendResult = await SendTransactionAsync(new SendTransactionParams()
        {
            Transaction = encodedSignedTransaction,
        }, tracker);
        if (sendResult.IsError)
        {
            tracker?.EndOperation();
            return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(sendResult);
        }
        SendTransactionResult send = sendResult.Value;
        if (send is not { ErrorResult: null })
        {
            tracker?.EndOperation();
            return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(StatusCode.TRANSACTION_SEND_FAILED, (sim, send, null), $"CallContractFunction {functionName} failed because the transaction sending result was not successful");
        }
        var getResult = await WaitForGetTransactionResult(send.Hash, 200, tracker);
        if (getResult.IsError)
        {
            tracker?.EndOperation();
            return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(getResult);
        }
        GetTransactionResult get = getResult.Value;
        tracker?.EndOperation();
        return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Ok((sim, send, get));
    }
    
    public static async Task<Result<(Transaction, SimulateTransactionResult)>> SimulateContractFunction(string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"SimulateContractFunction {functionName}");
        var accountEntryResult = await ReqAccountEntry(userAccount, tracker);
        if (accountEntryResult.IsError)
        {
            tracker?.EndOperation();
            return Result<(Transaction, SimulateTransactionResult)>.Err(accountEntryResult);
        }
        AccountEntry accountEntry = accountEntryResult.Value;
        List<SCVal> argsList = new() { userAddressSCVal };
        foreach (IScvMapCompatable arg in args)
        {
            argsList.Add(arg.ToScvMap());
        }
        Transaction invokeContractTransaction = BuildInvokeContractTransaction(accountEntry, functionName, argsList.ToArray(), true);
        var result = await SimulateTransactionAsync(
            new SimulateTransactionParams()
            {
                Transaction = EncodeTransaction(invokeContractTransaction),
                ResourceConfig = new(),
            },
            tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<(Transaction, SimulateTransactionResult)>.Err(result);
        }
        SimulateTransactionResult simulateTransactionResult = result.Value;
        if (simulateTransactionResult.Error != null)
        {
            tracker?.EndOperation();
            StatusCode code = HasContractError(simulateTransactionResult) ? StatusCode.CONTRACT_ERROR : StatusCode.SIMULATION_FAILED;
            return Result<(Transaction, SimulateTransactionResult)>.Err(code, (invokeContractTransaction, simulateTransactionResult), $"SimulateContractFunction {functionName} failed because the simulation result was not successful");
        }
        tracker?.EndOperation();
        return Result<(Transaction, SimulateTransactionResult)>.Ok((invokeContractTransaction, simulateTransactionResult));
    }
    
    public static async Task<Result<AccountEntry>> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqAccountEntry");
        string encodedKey = EncodedAccountKey(account);
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<AccountEntry>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
        if (getLedgerEntriesResult.Entries.Count != 1)
        {
            tracker?.EndOperation();
            return Result<AccountEntry>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqAccountEntry on {account} failed because there was not exactly one entry");
        }
        LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
        tracker?.EndOperation();
        return Result<AccountEntry>.Ok(entry?.account);
    }
    
    public static async Task<Result<LedgerEntry.dataUnion.Trustline>> GetAssets(MuxedAccount.KeyTypeEd25519 account, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetAssets");
        string encodedKey = EncodedTrustlineKey(account);
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<LedgerEntry.dataUnion.Trustline>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return Result<LedgerEntry.dataUnion.Trustline>.Err(StatusCode.ENTRY_NOT_FOUND, "GetAssets: no trustline entries found");
        }
        else
        {
            LedgerEntry.dataUnion.Trustline entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Trustline;
            tracker?.EndOperation();
            return Result<LedgerEntry.dataUnion.Trustline>.Ok(entry);
        }
    }
    
    public static async Task<Result<NetworkState>> ReqNetworkState(TimingTracker tracker = null)
    {
        tracker?.StartOperation("ReqNetworkState");
        NetworkState networkState = new(userAddress, GameManager.instance.IsOnline());
        
        var userResult = await ReqUser(userAddress, tracker);
        if (userResult.IsError)
        {
            tracker?.EndOperation();
            return Result<NetworkState>.Err(userResult);
        }
        User user = userResult.Value;
        networkState.user = user;
        if (user.current_lobby != 0)
        {
            var lobbyStuffResult = await ReqLobbyStuff(user.current_lobby, tracker);
            if (lobbyStuffResult.IsError)
            {
                tracker?.EndOperation();
                return Result<NetworkState>.Err(lobbyStuffResult);
            }
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = lobbyStuffResult.Value;
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
        }
        tracker?.EndOperation();
        return Result<NetworkState>.Ok(networkState);
    }
    
    public static async Task<Result<User>> ReqUser(AccountAddress key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqUser");
        // try to use the stellar rpc client
        LedgerKey ledgerKey = MakeLedgerKey("User", key, ContractDataDurability.PERSISTENT);
        string encodedKey = LedgerKeyXdr.EncodeToBase64(ledgerKey);
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedKey},
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<User>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return Result<User>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqUser: no entries found for key {key}");
        }
        Entries entries = getLedgerEntriesResult.Entries.First();
        if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
        {
            return Result<User>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqUserData on {key} failed because data was not ContractData");
        }
        User user = SCUtility.SCValToNative<User>(data.contractData.val);
        tracker?.EndOperation();
        return Result<User>.Ok(user);
    }

    public static async Task<Result<LobbyInfo>> ReqLobbyInfo(uint key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqLobbyStuff");
        string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyInfo", key, ContractDataDurability.TEMPORARY));
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                lobbyInfoKey,
            },
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<LobbyInfo>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return Result<LobbyInfo>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqLobbyInfo: no entries found for lobby {key}");
        }
        Entries entries = getLedgerEntriesResult.Entries.First();
        if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
        {
            tracker?.EndOperation();
            return Result<LobbyInfo>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqLobbyInfo on {key} failed because data was not ContractData");
        }
        LobbyInfo lobbyInfo = SCUtility.SCValToNative<LobbyInfo>(data.contractData.val);
        tracker?.EndOperation();
        return Result<LobbyInfo>.Ok(lobbyInfo);
    }
    
    static async Task<Result<(LobbyInfo?, LobbyParameters?, GameState?)>> ReqLobbyStuff(uint key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqLobbyStuff");
        string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyInfo", key, ContractDataDurability.TEMPORARY));
        string lobbyParametersKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyParameters", key, ContractDataDurability.TEMPORARY));
        string gameStateKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("GameState", key, ContractDataDurability.TEMPORARY));
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                lobbyInfoKey,
                lobbyParametersKey,
                gameStateKey,
            },
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<(LobbyInfo?, LobbyParameters?, GameState?)>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
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
                Debug.Log("wrote lobbyinfo");
            }
            else if (entry.Key == lobbyParametersKey)
            {
                LobbyParameters lobbyParameters = SCUtility.SCValToNative<LobbyParameters>(data.contractData.val);
                lobbyParameters.liveUntilLedgerSeq = entry.LiveUntilLedgerSeq;
                tuple.Item2 = lobbyParameters;
                Debug.Log("wrote lobbyparameters");
            }
            else if (entry.Key == gameStateKey)
            {
                GameState gameState = SCUtility.SCValToNative<GameState>(data.contractData.val);
                gameState.liveUntilLedgerSeq = entry.LiveUntilLedgerSeq;
                tuple.Item3 = gameState;
            }
        }
        tracker?.EndOperation();
        return Result<(LobbyInfo?, LobbyParameters?, GameState?)>.Ok(tuple);
    }
    
    static Transaction BuildInvokeContractTransaction(AccountEntry accountEntry, string functionName, SCVal[] args, bool increment)
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

    public static async Task<Result<PackedHistory>> ReqPackedHistory(uint key, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"ReqPackedHistory");
        var result = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new[]
            {
                LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("PackedHistory", key, ContractDataDurability.TEMPORARY)),
            },
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<PackedHistory>.Err(result);
        }
        GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            tracker?.EndOperation();
            return Result<PackedHistory>.Err(StatusCode.ENTRY_NOT_FOUND);
        }
        Entries entries = getLedgerEntriesResult.Entries.First();
        if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
        {
            tracker?.EndOperation();
            return Result<PackedHistory>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqPackedHistory on {key} failed because data was not ContractData");
        }
        PackedHistory packedHistory = SCUtility.SCValToNative<PackedHistory>(data.contractData.val);
        tracker?.EndOperation();
        return Result<PackedHistory>.Ok(packedHistory);
    }

    static string EncodeTransaction(Transaction transaction)
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

    static string SignAndEncodeTransaction(Transaction transaction)
    {
        if (isWallet)
        {
            // TODO: sign with WalletManager
        }
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

    static async Task<Result<GetTransactionResult>> WaitForGetTransactionResult(string txHash, int delayMS, TimingTracker tracker = null)
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
            var result = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash,
            }, tracker);
            if (result.IsError)
            {
                tracker?.EndOperation();
                return Result<GetTransactionResult>.Err(result);
            }
            GetTransactionResult completion = result.Value;
            switch (completion.Status)
            {
                case GetTransactionResultStatus.FAILED:
                    DebugLog("WaitForTransaction: FAILED");
                    tracker?.EndOperation();
                    return Result<GetTransactionResult>.Err(StatusCode.TRANSACTION_FAILED, completion.ResultMetaXdr);
                case GetTransactionResultStatus.NOT_FOUND:
                    tracker?.StartOperation($"Retry delay ({delayMS}ms)");
                    await AsyncDelay.Delay(delayMS);
                    tracker?.EndOperation();
                    continue;
                case GetTransactionResultStatus.SUCCESS:
                    DebugLog("WaitForTransaction: SUCCESS");
                    tracker?.EndOperation();
                    return Result<GetTransactionResult>.Ok(completion);
            }
        }
        DebugLog("WaitForTransaction: timed out");
        tracker?.EndOperation();
        return Result<GetTransactionResult>.Err(StatusCode.TRANSACTION_TIMEOUT);
        
    }
    
    
    // variant of StellarRPCClient.SimulateTransactionAsync()
    static async Task<Result<SimulateTransactionResult>> SimulateTransactionAsync(SimulateTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation("SimulateTransactionAsync");
        var result = await SendJsonRequest<SimulateTransactionResult>(new() 
        {
            JsonRpc = "2.0",
            Method = "simulateTransaction",
            Params = parameters,
            Id = 1,
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<SimulateTransactionResult>.Err(result);
        }
        SimulateTransactionResult transactionResult = result.Value;
        tracker?.EndOperation();
        return Result<SimulateTransactionResult>.Ok(transactionResult);
    }
    
    // variant of StellarRPCClient.SendTransactionAsync()
    static async Task<Result<SendTransactionResult>> SendTransactionAsync(SendTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"SendTransactionAsync");
        var result = await SendJsonRequest<SendTransactionResult>(new()
        {
            JsonRpc = "2.0",
            Method = "sendTransaction",
            Params = parameters,
            Id = 1,
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<SendTransactionResult>.Err(result);
        }
        SendTransactionResult transactionResult = result.Value;
        tracker?.EndOperation();
        return Result<SendTransactionResult>.Ok(transactionResult);
    }


    static async Task<Result<GetEventsResult>> GetEventsAsync(GetEventsParams parameters, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetEventsAsync");
        var result = await SendJsonRequest<GetEventsResult>(new() {
            JsonRpc = "2.0",
            Method = "getEvents",
            Params = parameters,
            Id = 1,
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<GetEventsResult>.Err(result);
        }
        GetEventsResult eventsResult = result.Value;
        latestLedger = eventsResult.LatestLedger;
        tracker?.EndOperation();
        return Result<GetEventsResult>.Ok(eventsResult);
    }
    
    // variant of StellarRPCClient.GetLedgerEntriesAsync()
    static async Task<Result<GetLedgerEntriesResult>> GetLedgerEntriesAsync(GetLedgerEntriesParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetLedgerEntriesAsync");
        var result = await SendJsonRequest<GetLedgerEntriesResult>(new()
        {
            JsonRpc = "2.0",
            Method = "getLedgerEntries",
            Params = parameters,
            Id = 1
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<GetLedgerEntriesResult>.Err(result);
        }
        GetLedgerEntriesResult ledgerEntriesAsync = result.Value;
        latestLedger = ledgerEntriesAsync.LatestLedger;
        tracker?.EndOperation();
        return Result<GetLedgerEntriesResult>.Ok(ledgerEntriesAsync);
    }
    
    // variant of StellarRPCClient.GetTransactionAsync()
    static async Task<Result<GetTransactionResult>> GetTransactionAsync(GetTransactionParams parameters = null, TimingTracker tracker = null)
    {
        tracker?.StartOperation($"GetTransactionAsync");
        var result = await SendJsonRequest<GetTransactionResult>(new()
        {
            JsonRpc = "2.0",
            Method = "getTransaction",
            Params = parameters,
            Id = 1
        }, tracker);
        if (result.IsError)
        {
            tracker?.EndOperation();
            return Result<GetTransactionResult>.Err(result);
        }
        GetTransactionResult transactionAsync = result.Value;
        latestLedger = transactionAsync.LatestLedger;
        tracker?.EndOperation();
        return Result<GetTransactionResult>.Ok(transactionAsync);
    }
    
    static async Task<Result<T>> SendJsonRequest<T>(JsonRpcRequest request, TimingTracker tracker = null)
    {
        tracker?.StartOperation("SendJsonRequest");
        string json = JsonConvert.SerializeObject(request, jsonSettings);
        UnityWebRequest unityWebRequest = new(networkUri, "POST") {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        unityWebRequest.SetRequestHeader("Content-Type", "application/json");
        DebugLog($"SendJsonRequest: request: {json}");
        tracker?.StartOperation("SendWebRequest");
        await unityWebRequest.SendWebRequest();
        tracker?.EndOperation();
        if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            return Result<T>.Err(StatusCode.NETWORK_ERROR, $"SendJsonRequest: error: {unityWebRequest.error}");
        }
        DebugLog($"SendJsonRequest: response: {unityWebRequest.downloadHandler.text}");
        string responseText = unityWebRequest.downloadHandler.text;
        
        T result = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(responseText, jsonSettings).Result;
        if (result == null)
        {
            return Result<T>.Err(StatusCode.DESERIALIZATION_ERROR, "SendJsonRequest: error: JSON deserialization failed");
        }
        tracker?.EndOperation();
        return Result<T>.Ok(result);
    }

    static bool HasContractError(SimulateTransactionResult simulate)
    {
        if (simulate == null || simulate.DiagnosticEvents == null) return false;
        foreach (var diag in simulate.DiagnosticEvents.Where(d => !d.inSuccessfulContractCall))
        {
            if (diag._event?.body is ContractEvent.bodyUnion.case_0 body)
            {
                foreach (SCVal topic in body.v0.topics)
                {
                    if (topic is SCVal.ScvError { error: SCError.SceContract })
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    static LedgerKey MakeLedgerKey(string sym, object key, ContractDataDurability durability)
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

    static string EncodedTrustlineKey(MuxedAccount.KeyTypeEd25519 account)
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

// Note: We rely on OverrideSpecifiedNames=false so any Newtonsoft [JsonProperty("...")] attributes
// from SDK types are honored, avoiding accidental casing changes like jsonrpc -> jsonRpc.

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

public enum StatusCode {
    SUCCESS,
    CONTRACT_ERROR,
    NETWORK_ERROR,
    RPC_ERROR,
    TIMEOUT,
    OTHER_ERROR,
    SERIALIZATION_ERROR,
    DESERIALIZATION_ERROR,
    TRANSACTION_FAILED,
    TRANSACTION_NOT_FOUND,
    TRANSACTION_TIMEOUT,
    ENTRY_NOT_FOUND,
    SIMULATION_FAILED,
    TRANSACTION_SEND_FAILED,
    WALLET_ERROR,
    WALLET_NOT_AVAILABLE,
    WALLET_ADDRESS_MISSING,
    WALLET_NETWORK_DETAILS_ERROR,
    WALLET_PARSING_ERROR,
}