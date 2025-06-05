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
    public string sneed = null;
    MuxedAccount.KeyTypeEd25519 userAccount => MuxedAccount.FromSecretSeed(sneed);
    public string userAddress => StrKey.EncodeStellarAccountId(userAccount.PublicKey);
    
    // contract address and derived properties
    public string contractAddress = null;
    

    Uri networkUri;
    // ReSharper disable once InconsistentNaming
    JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
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
        Debug.Log("CallVoidFunction called on " + functionName);
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        // make structs
        SCVal.ScvAddress addressArg = new SCVal.ScvAddress
        {
            address = new SCAddress.ScAddressTypeAccount()
            {
                accountId = new AccountID(userAccount.XdrPublicKey),
            },
        };
        List<SCVal> argsList = new List<SCVal>() { addressArg };
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
            Debug.LogError("CallVoidFunction timed out or failed to connect");
        }
        else if (getResult.Status != GetTransactionResultStatus.SUCCESS)
        {
            Debug.LogWarning("CallVoidFunction got " + getResult.Status);
        }
        // TODO: fix delay not working
        return (getResult, simResult);
    }
    
    public async Task<(SendTransactionResult, SimulateTransactionResult)> InvokeContractFunction(AccountEntry accountEntry, string functionName, SCVal[] args)
    {
        Transaction invokeContractTransaction = InvokeContractTransaction(functionName, accountEntry, args);
        SimulateTransactionResult simulateTransactionResult = await SimulateTransactionAsync(new SimulateTransactionParams()
        {
            Transaction = EncodeTransaction(invokeContractTransaction),
            ResourceConfig = new()
            {
                
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

    LedgerKey MakeLedgerKey(string sym, object key, ContractDataDurability durability)
    {
        SCVal scKey = SCUtility.NativeToSCVal(key);
        SCVal.ScvVec enumKey = new SCVal.ScvVec
        {
            vec = new SCVec(new SCVal[]
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

    public async Task<NetworkState> ReqNetworkState()
    {
        NetworkState networkState = new NetworkState(userAddress);
        User? mUser = await ReqUserData(userAddress);
        networkState.user = mUser;
        if (mUser is { current_lobby: not 0 })
        {
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = await ReqLobbyStuff(mUser.Value.current_lobby);
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
        }
        return networkState;
    }

    public async Task<NetworkState> ReqNetworkState(User? user)
    {
        NetworkState networkState = new NetworkState(userAddress);
        User? mUser = user;
        // if user doesnt exist yet
        if (!mUser.HasValue)
        {
            Debug.Log("ReqNetworkState: User is null so getting ReqUserData");
            // try get a user for mUser
            mUser = await ReqUserData(userAddress);
        }
        networkState.user = mUser;
        if (mUser is { current_lobby: not 0 })
        {
            (LobbyInfo? mLobbyInfo, LobbyParameters? mLobbyParameters, GameState? mGameState) = await ReqLobbyStuff(mUser.Value.current_lobby);
            networkState.lobbyInfo = mLobbyInfo;
            networkState.lobbyParameters = mLobbyParameters;
            networkState.gameState = mGameState;
            if (!mLobbyInfo.HasValue || !mLobbyParameters.HasValue || !mGameState.HasValue)
            {
                throw new Exception($"ReqNetworkState: Unable to get lobby entries for id {mUser.Value.current_lobby}");
            }
            // we got the entries for the current lobby according to the user but we have to check if we need to update the user
            bool userInLobby = false 
                               || Globals.AddressToString(mLobbyInfo?.host_address) == userAddress 
                               || Globals.AddressToString(mLobbyInfo?.guest_address) == userAddress;
            if (!userInLobby)
            {
                Debug.LogWarning("ReqNetworkState outdated user, retrying with user refresh");
                return await ReqNetworkState();
            }
        }
        else
        {
            Debug.Log("ReqNetworkState: User is either null or not in a lobby");
        }
        return networkState;
    }
    
    async Task<User?> ReqUserData(AccountAddress key)
    {
        LedgerKey ledgerKey = MakeLedgerKey("PackedUser", key, ContractDataDurability.PERSISTENT);
        Debug.Log("ReqUserData on " + key + " contract " + contractAddress);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new string[] {LedgerKeyXdr.EncodeToBase64(ledgerKey)},
        });
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.ContractData data = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.ContractData;
            if (data == null)
            {
                throw new Exception($"ReqUserData on {key} failed because data was not ContractData");
            }
            return SCUtility.SCValToNative<User>(data.contractData.val);
        }
    }

    async Task<(LobbyInfo?, LobbyParameters?, GameState?)> ReqLobbyStuff(uint key)
    {
        Debug.Log($"ReqLobbyInfo on {key} contract {contractAddress}");
        string lobbyInfoKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyInfo", key, ContractDataDurability.TEMPORARY));
        string lobbyParametersKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("LobbyParameters", key, ContractDataDurability.TEMPORARY));
        string gameStateKey = LedgerKeyXdr.EncodeToBase64(MakeLedgerKey("GameState", key, ContractDataDurability.TEMPORARY));
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new string[]
            {
                lobbyInfoKey,
                lobbyParametersKey,
                gameStateKey,
            },
        });
        (LobbyInfo?, LobbyParameters?, GameState?) tuple = (null, null, null);
        foreach (Entries entry in getLedgerEntriesResult.Entries)
        {
            LedgerEntry.dataUnion.ContractData data = entry.LedgerEntryData as LedgerEntry.dataUnion.ContractData;
            if (data == null)
            {
                throw new Exception($"ReqLobbyStuff on {key} failed because data was not ContractData");
            }
            if (entry.Key == lobbyInfoKey)
            {
                tuple.Item1 = SCUtility.SCValToNative<LobbyInfo>(data.contractData.val);
            }
            if (entry.Key == lobbyParametersKey)
            {
                tuple.Item2 = SCUtility.SCValToNative<LobbyParameters>(data.contractData.val);
            }
            if (entry.Key == gameStateKey)
            {
                tuple.Item3 = SCUtility.SCValToNative<GameState>(data.contractData.val);
            }
        }
        return tuple;
    }
    
    public async Task<Mailbox?> ReqMailData(string lobbyId)
    {
        Debug.Log("ReqMailData on " + lobbyId + " contract " + contractAddress);
        LedgerKey ledgerKey = MakeLedgerKey("Mail", lobbyId, ContractDataDurability.PERSISTENT);
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams
        {
            Keys = new string[] {LedgerKeyXdr.EncodeToBase64(ledgerKey)},
        });
        if (getLedgerEntriesResult.Entries.Count == 0)
        {
            return null;
        }
        else
        {
            LedgerEntry.dataUnion.ContractData data = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.ContractData;
            Mailbox mailbox = SCUtility.SCValToNative<Mailbox>(data.contractData.val);
            return mailbox;
        }
    }
    
    Transaction InvokeContractTransaction(string functionName, AccountEntry accountEntry, SCVal[] args)
    {
        Operation operation = new Operation()
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
        TransactionEnvelope.EnvelopeTypeTx envelope = new TransactionEnvelope.EnvelopeTypeTx()
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
        TransactionEnvelope.EnvelopeTypeTx envelope = new TransactionEnvelope.EnvelopeTypeTx()
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
        Debug.Log("WaitForTransaction started for txhash " + txHash);
        int max_attempts = 10;
        int attempts = 0;
        await AsyncDelay.Delay(delayMS);
        while (attempts < max_attempts)
        {
            Debug.Log("WaitForTransaction attempt " + attempts);
            GetTransactionResult completion = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash
            });
            switch (completion.Status)
            {
                case GetTransactionResultStatus.FAILED:
                    Debug.Log("WaitForTransaction FAILED");
                    return completion;
                case GetTransactionResultStatus.NOT_FOUND:
                    Debug.Log("WaitForTransaction waiting a bit");
                    await AsyncDelay.Delay(delayMS);
                    continue;
                case GetTransactionResultStatus.SUCCESS:
                    Debug.Log("WaitForTransaction SUCCESS");
                    return completion;
            }
            attempts++;
        }
        Debug.Log("WaitForTransaction timed out");
        return null;
    }
    
    
    // variant of StellarRPCClient.SimulateTransactionAsync()
    async Task<SimulateTransactionResult> SimulateTransactionAsync(SimulateTransactionParams parameters = null)
    {
        JsonRpcRequest request = new JsonRpcRequest()
        {
            JsonRpc = "2.0",
            Method = "simulateTransaction",
            Params = (object) parameters,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject((object) request, this._jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JObject jsonObject = JObject.Parse(content);
        // Remove "stateChanges" entirely to avoid deserialization issues
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
            throw e;
        }

    }
    
    // variant of StellarRPCClient.SendTransactionAsync()
    async Task<SendTransactionResult> SendTransactionAsync(SendTransactionParams parameters = null)
    {
        JsonRpcRequest request = new JsonRpcRequest()
        {
            JsonRpc = "2.0",
            Method = "sendTransaction",
            Params = (object) parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject((object) request, this._jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<SendTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<SendTransactionResult>>(content, this._jsonSettings);
        SendTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        return transactionResult;
    }
    
    // variant of StellarRPCClient.GetLedgerEntriesAsync()
    async Task<GetLedgerEntriesResult> GetLedgerEntriesAsync(GetLedgerEntriesParams parameters = null)
    {
        JsonRpcRequest request = new JsonRpcRequest()
        {
            JsonRpc = "2.0",
            Method = "getLedgerEntries",
            Params = (object) parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject((object) request, this._jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<GetLedgerEntriesResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLedgerEntriesResult>>(content);
        GetLedgerEntriesResult ledgerEntriesAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        long currentLedger = ledgerEntriesAsync.LatestLedger;
        Debug.Log($"GetLedgerEntriesAsync: currentLedger = {currentLedger}");
        foreach (var entry in ledgerEntriesAsync.Entries)
        {
            var ledgerLeft = entry.LiveUntilLedgerSeq - currentLedger;
            var timeLeft = ledgerLeft * 5;
            var timeString = TimeSpan.FromSeconds(timeLeft).ToString(@"hh\:mm\:ss");
            Debug.Log($"GetLedgerEntriesAsync: ledger key: {entry.Key} live until: {entry.LiveUntilLedgerSeq} ledgers left: {ledgerLeft} time left: {timeString}");
        }
        return ledgerEntriesAsync;
    }
    
    // variant of StellarRPCClient.GetTransactionAsync()
    async Task<GetTransactionResult> GetTransactionAsync(GetTransactionParams parameters = null)
    {
        JsonRpcRequest request = new JsonRpcRequest()
        {
            JsonRpc = "2.0",
            Method = "getTransaction",
            Params = (object) parameters,
            Id = 1
        };
        string requestJson = JsonConvert.SerializeObject((object) request, this._jsonSettings);
        string content = await SendJsonRequest(requestJson);
        JsonRpcResponse<GetTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetTransactionResult>>(content, this._jsonSettings);
        GetTransactionResult transactionAsync = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        return transactionAsync;
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
    
    async Task<string> SendJsonRequest(string json)
    {
        UnityWebRequest request = new UnityWebRequest(networkUri, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Debug.Log("SendJsonRequest sending off: " + json);
        await request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            Debug.Log("Response: " + request.downloadHandler.text);
        }
        return request.downloadHandler.text;
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

    public string EncodedTrustlineKey(MuxedAccount.KeyTypeEd25519 account)
    {
        string code = "SCRY";
        string issuerAccountId = "GAAPZLAZJ5SL4IL63WHFWRUWPK2UV4SREUOWM2DZTTQR7FJPFQAHDSNG";
        AccountID issuerAccount = MuxedAccount.FromAccountId(issuerAccountId).XdrPublicKey;
        byte[] codeBytes = System.Text.Encoding.ASCII.GetBytes(code);
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
    
    public static bool IsValidStellarAddress(string address)
    {
        // Check if the address is null, empty, or whitespace.
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // Check that the address starts with "G" (as expected for Stellar public keys).
        if (!address.StartsWith("G"))
            return false;

        // The address should be exactly 56 characters in its Base32-encoded form.
        if (address.Length != 56)
            return false;

        try
        {
            // Decode the Base32 encoded string to get the underlying binary data.
            byte[] decoded = Base32Decode(address);
            
            // The decoded binary should be exactly 35 bytes:
            // 1-byte version + 32-byte public key + 2-byte checksum.
            if (decoded.Length != 35)
                return false;

            // Check the version byte (for public keys, it should be 6 << 3 which is 48).
            if (decoded[0] != 48)
                return false;

            // Verify the checksum:
            // Extract the checksum from the last two bytes.
            ushort checksum = BitConverter.ToUInt16(decoded, decoded.Length - 2);

            // Compute the CRC16-XModem checksum for the first 33 bytes (version byte + key).
            ushort computedChecksum = CalculateCRC16(decoded.Take(33).ToArray());
            
            // Return true only if the checksums match.
            return checksum == computedChecksum;
        }
        catch (Exception)
        {
            // If any exception occurs (for example, during decoding), the address is invalid.
            return false;
        }
    }

    /// <summary>
    /// Decodes a Base32 encoded string using the alphabet "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".
    /// Stellar’s StrKey encoding uses this alphabet (without padding).
    /// </summary>
    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // Remove any padding if present (typically Stellar addresses do not include '=' padding).
        input = input.TrimEnd('=');
        int byteCount = input.Length * 5 / 8;
        byte[] output = new byte[byteCount];

        int bitBuffer = 0;
        int bitsLeft = 0;
        int index = 0;
        foreach (char c in input)
        {
            int value = alphabet.IndexOf(c);
            if (value < 0)
                throw new ArgumentException("Invalid character encountered in the address.");

            bitBuffer = (bitBuffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                // Take the top 8 bits out of the buffer.
                output[index++] = (byte)((bitBuffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }
        return output;
    }

    /// <summary>
    /// Calculates the CRC16-XModem checksum for the provided data.
    /// </summary>
    private static ushort CalculateCRC16(byte[] data)
    {
        ushort crc = 0;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc <<= 1;
            }
        }
        return crc;
    }
}

public static class AsyncDelay
{
    public static Task Delay(int millisecondsDelay)
    {
        // Use a coroutine-based delay in WebGL
        return WaitForSecondsAsync(millisecondsDelay / 1000f);
    }

    private static Task WaitForSecondsAsync(float seconds)
    {
        var tcs = new TaskCompletionSource<bool>();
        // CoroutineRunner is a MonoBehaviour that can run coroutines.
        CoroutineRunner.Instance.StartCoroutine(WaitForSecondsCoroutine(seconds, tcs));
        return tcs.Task;
    }

    private static IEnumerator WaitForSecondsCoroutine(float seconds, TaskCompletionSource<bool> tcs)
    {
        yield return new WaitForSeconds(seconds);
        tcs.SetResult(true);
    }
}

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;
    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create a new GameObject to attach the runner
                var go = new GameObject("CoroutineRunner");
                _instance = go.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
}