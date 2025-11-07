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
    static UnityWebRequest activeRequest;
    static long latestLedger;
    
    static void DebugLog(string message)
    {
        if (ResourceRoot.DefaultSettings.networkLogging)
        {
            Debug.Log(message);
        }
    }
    // contract address and derived properties
    // public static string contractAddress;
    const int maxAttempts = 30;
    static readonly byte[] exceededLimitPatternLower = Encoding.ASCII.GetBytes("exceeded_limit");
    static readonly byte[] exceededLimitPatternUpper = Encoding.ASCII.GetBytes("EXCEEDED_LIMIT");
    static readonly byte[] outOfFuelPattern = Encoding.ASCII.GetBytes("OutOfFuel");
    
    // public static Uri networkUri;
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


    public static async Task<Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>> CallContractFunction(NetworkContext context, string functionName, IScvMapCompatable arg, TimingTracker tracker = null)
    {
        return await CallContractFunction(context, functionName, new IScvMapCompatable[] {arg}, tracker);
    }
    
    public static async Task<Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>> CallContractFunction(NetworkContext context, string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"CallContractFunction {functionName}"))
        {
            bool retryAllowed = true;
            int attempt = 0;
            while (true)
            {
                attempt++;
                var simResult = await SimulateContractFunction(context, functionName, args, tracker);
                if (simResult.IsError)
                {
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(simResult);
                }
                (Transaction transaction, SimulateTransactionResult sim) = simResult.Value;
                if (sim is not { Error: null })
                {
                    StatusCode code = HasContractError(sim) ? StatusCode.CONTRACT_ERROR : StatusCode.SIMULATION_FAILED;
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(code, (sim, null, null), $"CallContractFunction {functionName} failed because the simulation result was not successful");
                }
                // increase MinResourceFee if function is one that can have a race condition
                List<string> raceConditionFunctions = new() { 
                    "commit_move_and_prove_move",
                    "prove_move_and_prove_rank",
                    "commit_move",
                    "prove_move",
                    "prove_rank",
                    "commit_setup",
                    "leave_lobby",
                };
                if (raceConditionFunctions.Contains(functionName))
                {
                    Debug.Log($"{functionName} {sim.MinResourceFee} {transaction.fee.InnerValue}");
                    long minResourceFee = long.Parse(sim.MinResourceFee) * 2;
                    sim.MinResourceFee = minResourceFee.ToString();
                    Debug.Log($"Increased MinResourceFee for {functionName} to {sim.MinResourceFee}");
                }
                Transaction assembledTransaction = sim.ApplyTo(transaction);
                Result<string> signResult = await SignAndEncodeTransaction(context, assembledTransaction);
                if (signResult.IsError)
                {
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(StatusCode.WALLET_ERROR, (sim, null, null), $"CallContractFunction {functionName} failed because failed to sign");
                }

                var sendResult = await SendTransactionAsync(context, new SendTransactionParams()
                {
                    Transaction = signResult.Value,
                }, tracker);
                if (sendResult.IsError)
                {
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(sendResult);
                }

                SendTransactionResult send = sendResult.Value;
                if (send is not { ErrorResult: null })
                {
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(StatusCode.TRANSACTION_SEND_FAILED, (sim, send, null), $"CallContractFunction {functionName} failed because the transaction sending result was not successful");
                }

                var getResult = await WaitForGetTransactionResult(context, send.Hash, 200, tracker);
                if (getResult.IsError)
                {
                    if (retryAllowed && getResult.Code == StatusCode.TRANSACTION_FAILED && IsExceededLimitError(getResult.Value))
                    {
                        retryAllowed = false;
                        Debug.LogWarning($"CallContractFunction {functionName}: detected exceeded_limit after attempt {attempt}, retrying once.");
                        continue;
                    }
                    return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Err(getResult);
                }

                GetTransactionResult get = getResult.Value;
                return Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)>.Ok((sim, send, get));
            }
        }
    }
    
    public static async Task<Result<(Transaction, SimulateTransactionResult)>> SimulateContractFunction(NetworkContext context, string functionName, IScvMapCompatable[] args, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"SimulateContractFunction {functionName}"))
        {
            var accountEntryResult = await ReqAccountEntry(context, tracker);
            if (accountEntryResult.IsError)
            {
                return Result<(Transaction, SimulateTransactionResult)>.Err(accountEntryResult);
            }
        AccountEntry accountEntry = accountEntryResult.Value;
        // TODO: replace this horrible line
        List<SCVal> argsList = new() { new SCVal.ScvAddress() { address = new SCAddress.ScAddressTypeAccount() { accountId = new AccountID(context.userAccount.XdrPublicKey) } } };
        foreach (IScvMapCompatable arg in args)
        {
            argsList.Add(arg.ToScvMap());
        }
        Transaction invokeContractTransaction = BuildInvokeContractTransaction(context, accountEntry, functionName, argsList.ToArray(), true);
        var result = await SimulateTransactionAsync(context, new SimulateTransactionParams()
            {
                Transaction = EncodeTransaction(invokeContractTransaction),
                ResourceConfig = new(),
            },
            tracker);
            if (result.IsError)
            {
                return Result<(Transaction, SimulateTransactionResult)>.Err(result);
            }
        SimulateTransactionResult simulateTransactionResult = result.Value;
        if (simulateTransactionResult.Error != null)
        {
            StatusCode code = HasContractError(simulateTransactionResult) ? StatusCode.CONTRACT_ERROR : StatusCode.SIMULATION_FAILED;
            return Result<(Transaction, SimulateTransactionResult)>.Err(code, (invokeContractTransaction, simulateTransactionResult), $"SimulateContractFunction {functionName} failed because the simulation result was not successful");
        }
        return Result<(Transaction, SimulateTransactionResult)>.Ok((invokeContractTransaction, simulateTransactionResult));
        }
    }
    
    public static async Task<Result<AccountEntry>> ReqAccountEntry(NetworkContext context, TimingTracker tracker = null)
    {
        using (tracker?.Scope("ReqAccountEntry"))
        {
            string encodedKey = EncodedAccountKey(context);
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams()
            {
                Keys = new [] {encodedKey},
            }, tracker);
            if (result.IsError)
            {
                return Result<AccountEntry>.Err(result);
            }
            GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
            if (getLedgerEntriesResult.Entries.Count != 1)
            {
                return Result<AccountEntry>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqAccountEntry on {context.userAccount.XdrPublicKey} failed because there was not exactly one entry");
            }
            LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
            return Result<AccountEntry>.Ok(entry?.account);
        }
    }
    
    public static async Task<Result<LedgerEntry.dataUnion.Trustline>> GetAssets(NetworkContext context, TimingTracker tracker = null)
    {
        using (tracker?.Scope("GetAssets"))
        {
            string encodedKey = EncodedTrustlineKey(context);
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams()
            {
                Keys = new [] {encodedKey},
            }, tracker);
            if (result.IsError)
            {
                return Result<LedgerEntry.dataUnion.Trustline>.Err(result);
            }
            GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
            if (getLedgerEntriesResult.Entries.Count == 0)
            {
                return Result<LedgerEntry.dataUnion.Trustline>.Err(StatusCode.ENTRY_NOT_FOUND, "GetAssets: no trustline entries found");
            }
            else
            {
                LedgerEntry.dataUnion.Trustline entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Trustline;
                return Result<LedgerEntry.dataUnion.Trustline>.Ok(entry);
            }
        }
    }
    
    public static async Task<Result<NetworkState>> ReqNetworkState(NetworkContext networkContext, TimingTracker tracker = null)
    {
        using (tracker?.Scope("ReqNetworkState"))
        {
            NetworkState networkState = new(networkContext);
            User? user = null;
            LobbyInfo? lobbyInfo = null;
            LobbyParameters? lobbyParameters = null;
            GameState? gameState = null;

            var userResult = await ReqUser(networkContext, tracker);
            if (userResult.IsError)
            {
                if (userResult.Code == StatusCode.ENTRY_NOT_FOUND)
                {
                    // because there is no user, we can return a empty network state
                    return Result<NetworkState>.Ok(networkState);
                }
                return Result<NetworkState>.Err(userResult);
            }
            user = userResult.Value;
            LobbyId currentLobby = user.Value.current_lobby;
            if (currentLobby != 0)
            {
                var lobbyStuffResult = await ReqLobbyStuff(networkContext, currentLobby, tracker);
                if (lobbyStuffResult.IsError)
                {
                    return Result<NetworkState>.Err(lobbyStuffResult);
                }
                (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = lobbyStuffResult.Value;
                lobbyInfo = mLobbyInfo;
                lobbyParameters = mLobbyParameters;
                gameState = mGameState;
            }

            networkState.user = user;
            networkState.lobbyInfo = lobbyInfo;
            networkState.lobbyParameters = lobbyParameters;
            networkState.gameState = gameState;
            return Result<NetworkState>.Ok(networkState);
        }
    }
    
    public static async Task<Result<User>> ReqUser(NetworkContext context, TimingTracker tracker = null)
    {
        using (tracker?.Scope("ReqUser"))
        {
            // try to use the stellar rpc client
            LedgerKey ledgerKey = MakeLedgerKey(context, "User", new AccountAddress(context.userAccount.Address), ContractDataDurability.PERSISTENT);
            string encodedKey = LedgerKeyXdr.EncodeToBase64(ledgerKey);
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams()
            {
                Keys = new [] {encodedKey},
            }, tracker);
            if (result.IsError)
            {
                return Result<User>.Err(result);
            }
            GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
            if (getLedgerEntriesResult.Entries.Count == 0)
            {
                // it's normal to not find entries for users
                return Result<User>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqUser: no entries found for key {context.userAccount.AccountId}");
            }
            Entries entries = getLedgerEntriesResult.Entries.First();
            if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
            {
                return Result<User>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqUserData on {context.userAccount.AccountId} failed because data was not ContractData");
            }
            User user = SCUtility.SCValToNative<User>(data.contractData.val);
            return Result<User>.Ok(user);
        }
    }

    public static async Task<Result<LobbyInfo>> ReqLobbyInfo(NetworkContext context, uint key, TimingTracker tracker = null)
    {
        using (tracker?.Scope("ReqLobbyStuff"))
        {
            string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey(context, "LobbyInfo", key, ContractDataDurability.TEMPORARY));
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams
            {
                Keys = new[]
                {
                    lobbyInfoKey,
                },
            }, tracker);
            if (result.IsError)
            {
                return Result<LobbyInfo>.Err(result);
            }
            GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
            if (getLedgerEntriesResult.Entries.Count == 0)
            {
                return Result<LobbyInfo>.Err(StatusCode.ENTRY_NOT_FOUND, $"ReqLobbyInfo: no entries found for lobby {key}");
            }
            Entries entries = getLedgerEntriesResult.Entries.First();
            if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
            {
                return Result<LobbyInfo>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqLobbyInfo on {key} failed because data was not ContractData");
            }
            LobbyInfo lobbyInfo = SCUtility.SCValToNative<LobbyInfo>(data.contractData.val);
            return Result<LobbyInfo>.Ok(lobbyInfo);
        }
    }
    
    static async Task<Result<(LobbyInfo?, LobbyParameters?, GameState?)>> ReqLobbyStuff(NetworkContext context, uint key, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"ReqLobbyStuff"))
        {
            string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey(context, "LobbyInfo", key, ContractDataDurability.TEMPORARY));
            string lobbyParametersKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey(context, "LobbyParameters", key, ContractDataDurability.TEMPORARY));
            string gameStateKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey(context, "GameState", key, ContractDataDurability.TEMPORARY));
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams
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
            return Result<(LobbyInfo?, LobbyParameters?, GameState?)>.Ok(tuple);
        }
    }
    
    static Transaction BuildInvokeContractTransaction(NetworkContext context, AccountEntry accountEntry, string functionName, SCVal[] args, bool increment)
    {
        List<Operation> operations = new();
        Operation operation = new()
        {
            sourceAccount = context.userAccount,
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
                                contractId = new Hash(StrKey.DecodeContractId(context.contractAddress)),
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
            sourceAccount = operation.sourceAccount,
            fee = 100000, // TODO: make this configurable
            memo = new Memo.MemoNone(),
            seqNum = accountEntry.seqNum, 
            cond = new Preconditions.PrecondNone(),
            ext = new Transaction.extUnion.case_0(),
            operations = operations.ToArray(),
        };
    }

    public static async Task<Result<PackedHistory>> ReqPackedHistory(NetworkContext context, uint key, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"ReqPackedHistory"))
        {
            var result = await GetLedgerEntriesAsync(context, new GetLedgerEntriesParams
            {
                Keys = new[]
                {
                    LedgerKeyXdr.EncodeToBase64(MakeLedgerKey(context, "PackedHistory", key, ContractDataDurability.TEMPORARY)),
                },
            }, tracker);
            if (result.IsError)
            {
                return Result<PackedHistory>.Err(result);
            }
            GetLedgerEntriesResult getLedgerEntriesResult = result.Value;
            if (getLedgerEntriesResult.Entries.Count == 0)
            {
                return Result<PackedHistory>.Err(StatusCode.ENTRY_NOT_FOUND);
            }
            Entries entries = getLedgerEntriesResult.Entries.First();
            if (entries.LedgerEntryData is not LedgerEntry.dataUnion.ContractData data)
            {
                return Result<PackedHistory>.Err(StatusCode.SERIALIZATION_ERROR, $"ReqPackedHistory on {key} failed because data was not ContractData");
            }
            PackedHistory packedHistory = SCUtility.SCValToNative<PackedHistory>(data.contractData.val);
            return Result<PackedHistory>.Ok(packedHistory);
        }
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

    static async Task<Result<string>> SignAndEncodeTransaction(NetworkContext context, Transaction transaction)
    {
        if (context.isWallet)
        {
            Result<string> signTransactionRes = await WalletManager.SignTransaction(EncodeTransaction(transaction), Network.Current.NetworkPassphrase);
            if (signTransactionRes.IsError)
            {
                return Result<string>.Err(signTransactionRes);
            }
            return Result<string>.Ok(signTransactionRes.Value);
        }
        else
        {
            DecoratedSignature signature = transaction.Sign(context.userAccount);
            TransactionEnvelope.EnvelopeTypeTx envelope = new()
            {
                v1 = new TransactionV1Envelope()
                {
                    tx = transaction,
                    signatures = new[] { signature },
                },
            };
            return Result<string>.Ok(TransactionEnvelopeXdr.EncodeToBase64(envelope));
        }
    }

    static async Task<Result<GetTransactionResult>> WaitForGetTransactionResult(NetworkContext context, string txHash, int delayMS, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"WaitForGetTransactionResult"))
        {
            int attempts = 0;
            using (tracker?.Scope($"Initial delay ({delayMS}ms)"))
            {
                await AsyncDelay.Delay(delayMS);
            }
            while (attempts < maxAttempts)
            {
                attempts++;
                var result = await GetTransactionAsync(context, new GetTransactionParams()
                {
                    Hash = txHash,
                }, tracker);
                if (result.IsError)
                {
                    return Result<GetTransactionResult>.Err(result);
                }
                GetTransactionResult completion = result.Value;
                switch (completion.Status)
                {
                    case GetTransactionResultStatus.FAILED:
                        DebugLog("WaitForTransaction: FAILED");
                        string failureMessage = completion.ResultMetaXdr;
                        return Result<GetTransactionResult>.Err(StatusCode.TRANSACTION_FAILED, completion, failureMessage);
                    case GetTransactionResultStatus.NOT_FOUND:
                        using (tracker?.Scope($"Retry delay ({delayMS}ms)"))
                        {
                            await AsyncDelay.Delay(delayMS);
                        }
                        continue;
                    case GetTransactionResultStatus.SUCCESS:
                        DebugLog("WaitForTransaction: SUCCESS");
                        return Result<GetTransactionResult>.Ok(completion);
                }
            }
            DebugLog("WaitForTransaction: timed out");
            return Result<GetTransactionResult>.Err(StatusCode.TRANSACTION_TIMEOUT);
        }
    }
    
    
    static bool IsExceededLimitError(GetTransactionResult getResult)
    {
        if (getResult == null)
        {
            return false;
        }

        if (TryDecodeBase64(getResult.ResultMetaXdr, out byte[] data))
        {
            if (ByteArrayContainsSequence(data, exceededLimitPatternLower) ||
                ByteArrayContainsSequence(data, exceededLimitPatternUpper) ||
                ByteArrayContainsSequence(data, outOfFuelPattern))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryDecodeBase64(string base64, out byte[] data)
    {
        if (string.IsNullOrEmpty(base64))
        {
            data = null;
            return false;
        }

        try
        {
            data = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            data = null;
            return false;
        }
    }

    static bool ByteArrayContainsSequence(byte[] haystack, byte[] needle)
    {
        if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            for (; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    break;
                }
            }

            if (j == needle.Length)
            {
                return true;
            }
        }

        return false;
    }


    // variant of StellarRPCClient.SimulateTransactionAsync()
    static async Task<Result<SimulateTransactionResult>> SimulateTransactionAsync(NetworkContext context, SimulateTransactionParams parameters = null, TimingTracker tracker = null)
    {
        using (tracker?.Scope("SimulateTransactionAsync"))
        {
            var result = await SendJsonRequest<SimulateTransactionResult>(context, new() 
            {
                JsonRpc = "2.0",
                Method = "simulateTransaction",
                Params = parameters,
                Id = 1,
            }, tracker);
            if (result.IsError)
            {
                return Result<SimulateTransactionResult>.Err(result);
            }
            SimulateTransactionResult transactionResult = result.Value;
            return Result<SimulateTransactionResult>.Ok(transactionResult);
        }
    }
    
    // variant of StellarRPCClient.SendTransactionAsync()
    static async Task<Result<SendTransactionResult>> SendTransactionAsync(NetworkContext context, SendTransactionParams parameters = null, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"SendTransactionAsync"))
        {
            var result = await SendJsonRequest<SendTransactionResult>(context, new()
            {
                JsonRpc = "2.0",
                Method = "sendTransaction",
                Params = parameters,
                Id = 1,
            }, tracker);
            if (result.IsError)
            {
                return Result<SendTransactionResult>.Err(result);
            }
            SendTransactionResult transactionResult = result.Value;
            return Result<SendTransactionResult>.Ok(transactionResult);
        }
    }


    static async Task<Result<GetEventsResult>> GetEventsAsync(NetworkContext context, GetEventsParams parameters, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"GetEventsAsync"))
        {
            var result = await SendJsonRequest<GetEventsResult>(context, new() {
                JsonRpc = "2.0",
                Method = "getEvents",
                Params = parameters,
                Id = 1,
            }, tracker);
            if (result.IsError)
            {
                return Result<GetEventsResult>.Err(result);
            }
            GetEventsResult eventsResult = result.Value;
            latestLedger = eventsResult.LatestLedger;
            return Result<GetEventsResult>.Ok(eventsResult);
        }
    }
    
    // variant of StellarRPCClient.GetLedgerEntriesAsync()
    static async Task<Result<GetLedgerEntriesResult>> GetLedgerEntriesAsync(NetworkContext context, GetLedgerEntriesParams parameters = null, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"GetLedgerEntriesAsync"))
        {
            var result = await SendJsonRequest<GetLedgerEntriesResult>(context, new()
            {
                JsonRpc = "2.0",
                Method = "getLedgerEntries",
                Params = parameters,
                Id = 1
            }, tracker);
            if (result.IsError)
            {
                return Result<GetLedgerEntriesResult>.Err(result);
            }
            GetLedgerEntriesResult ledgerEntriesAsync = result.Value;
            latestLedger = ledgerEntriesAsync.LatestLedger;
            return Result<GetLedgerEntriesResult>.Ok(ledgerEntriesAsync);
        }
    }
    
    // variant of StellarRPCClient.GetTransactionAsync()
    static async Task<Result<GetTransactionResult>> GetTransactionAsync(NetworkContext context, GetTransactionParams parameters = null, TimingTracker tracker = null)
    {
        using (tracker?.Scope($"GetTransactionAsync"))
        {
            var result = await SendJsonRequest<GetTransactionResult>(context, new()
            {
                JsonRpc = "2.0",
                Method = "getTransaction",
                Params = parameters,
                Id = 1
            }, tracker);
            if (result.IsError)
            {
                return Result<GetTransactionResult>.Err(result);
            }
            GetTransactionResult transactionAsync = result.Value;
            latestLedger = transactionAsync.LatestLedger;
            return Result<GetTransactionResult>.Ok(transactionAsync);
        }
    }
    
    static async Task<Result<T>> SendJsonRequest<T>(NetworkContext context, JsonRpcRequest request, TimingTracker tracker = null)
    {
        using (tracker?.Scope("SendJsonRequest"))
        {
            string json = JsonConvert.SerializeObject(request, jsonSettings);
            UnityWebRequest unityWebRequest = new(context.serverUri, "POST") {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            unityWebRequest.SetRequestHeader("Content-Type", "application/json");
            DebugLog($"SendJsonRequest: request: {json}");
            using (tracker?.Scope("SendWebRequest"))
            {
                activeRequest = unityWebRequest;
                try
                {
                    await unityWebRequest.SendWebRequest();
                }
                finally
                {
                    if (ReferenceEquals(activeRequest, unityWebRequest))
                    {
                        activeRequest = null;
                    }
                }
            }
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
            return Result<T>.Ok(result);
        }
    }

    public static void AbortActiveRequest()
    {
        try
        {
            activeRequest?.Abort();
        }
        catch (Exception)
        {
            // Swallow any abort exceptions; request may already be finished/disposed
        }
        finally
        {
            activeRequest = null;
        }
    }

    public static void ResetStatic()
    {
        AbortActiveRequest();
        latestLedger = 0;
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
    
    static LedgerKey MakeLedgerKey(NetworkContext context, string sym, object key, ContractDataDurability durability)
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
                    contractId = new Hash(StrKey.DecodeContractId(context.contractAddress)),
                },
                key = enumKey,
                durability = durability,
            },
        };
    }

    static string EncodedAccountKey(NetworkContext context)
    {
        return LedgerKeyXdr.EncodeToBase64(new LedgerKey.Account()
        {
            account = new LedgerKey.accountStruct()
            {
                accountID = context.userAccount.XdrPublicKey,
            },
        });
    }

    static string EncodedTrustlineKey(NetworkContext context)
    {
        string code = "SCRY";
        string issuerAccountId = "GAAPZLAZJ5SL4IL63WHFWRUWPK2UV4SREUOWM2DZTTQR7FJPFQAHDSNG";
        AccountID issuerAccount = MuxedAccount.FromAccountId(issuerAccountId).XdrPublicKey;
        byte[] codeBytes = Encoding.ASCII.GetBytes(code);
        return LedgerKeyXdr.EncodeToBase64(new LedgerKey.Trustline
        {
            trustLine = new LedgerKey.trustLineStruct
            {
                accountID = context.userAccount.XdrPublicKey,
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
    
    public readonly struct OperationScope : IDisposable
    {
        readonly TimingTracker tracker;
        public OperationScope(TimingTracker tracker, string name)
        {
            this.tracker = tracker;
            tracker?.StartOperation(name);
        }
        public void Dispose()
        {
            tracker?.EndOperation();
        }
    }
    
    public OperationScope Scope(string name)
    {
        return new OperationScope(this, name);
    }
    
    public static OperationScope Scope(TimingTracker tracker, string name)
    {
        return new OperationScope(tracker, name);
    }
    
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
    WALLET_SIGNING_ERROR,
    CLIENT_BOARD_VALIDATION_ERROR,
}