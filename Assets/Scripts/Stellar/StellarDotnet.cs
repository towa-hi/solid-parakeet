using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ContractTypes;
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
    public string contractId;
    MuxedAccount.KeyTypeEd25519 userAccount;
    AccountID accountId => new AccountID(userAccount.XdrPublicKey);
    
    Uri networkUri;
    // ReSharper disable once InconsistentNaming
    JsonSerializerSettings _jsonSettings;
    
    public StellarDotnet(string inSecretSneed, string inContractId)
    {
        networkUri = new Uri("https://soroban-testnet.stellar.org");
        Network.UseTestNetwork();
        SetUserAccount(inSecretSneed);
        SetContractId(inContractId);
        _jsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
    
    public void SetContractId(string inContractId)
    {
        contractId = inContractId;
    }

    public void SetUserAccount(string inSecretSneed)
    {
        userAccount = MuxedAccount.FromSecretSeed(inSecretSneed);
    }

    public async Task<bool> SendInvite(SendInviteReq req)
    {
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        SCVal reqArg = req.ToScvMap();
        SCVal.ScvAddress addressArg = new SCVal.ScvAddress
        {
            address = new SCAddress.ScAddressTypeAccount()
            {
                accountId = accountId,
            },
        };
        SCVal[] args = {addressArg, reqArg };
        SendTransactionResult result = await InvokeContractFunction(accountEntry, "send_invite", args);
        Debug.Log("transaction hash " + result.Hash);
        GetTransactionResult getResult = await WaitForTransaction(result.Hash, 2000);
        if (getResult == null)
        {
            Debug.LogError("get transaction failed");
            return false;
        }
        return true;
    }
    
    public async Task<bool> TestFunction(SCVal data)
    {
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        // make structs
        SCVal.ScvAddress addressArg = new SCVal.ScvAddress
        {
            address = new SCAddress.ScAddressTypeAccount()
            {
                accountId = accountId,
            },
        };
        SCVal[] args = {addressArg, data };
        SendTransactionResult result = await InvokeContractFunction(accountEntry, "send_invite", args);
        Debug.Log("transaction hash " + result.Hash);
        GetTransactionResult getResult = await WaitForTransaction(result.Hash, 2000);
        if (getResult == null)
        {
            Debug.LogError("get transaction failed");
            return false;
        }
        Debug.Log(getResult.Status);
        // TODO: fix delay not working
        // SCVal returnValue = (getResult.TransactionResultMeta as TransactionMeta.case_3).v3.sorobanMeta.returnValue;
        // SendInviteReq decoded = SCValConverter.SCValToNative<SendInviteReq>(returnValue);
        // Debug.Log(decoded);
        // SCVal arg2 = SCValConverter.NativeToSCVal(decoded);
        // SCVal[] args2 = { addressArg, arg2 };
        // SendTransactionResult result2 = await InvokeContractFunction(accountEntry, "nested_param_test", args2);
        // GetTransactionResult getResult2 = await WaitForTransaction(result.Hash, 2000);
        // if (getResult2 == null)
        // {
        //     Debug.LogError("get transaction failed");
        //     return false;
        // }
        // SCVal returnValue2 = (getResult2.TransactionResultMeta as TransactionMeta.case_3).v3.sorobanMeta.returnValue;
        // NestedTestReq decoded2 = SCValConverter.SCValToNative<NestedTestReq>(returnValue2);
        // if (SCValConverter.HashEqual(returnValue, returnValue2))
        // {
        //     Debug.Log("test good");
        // }
        return true;
    }
    
    public async Task<SendTransactionResult> InvokeContractFunction(AccountEntry accountEntry, string functionName, SCVal[] args)
    {
        Transaction invokeContractTransaction = InvokeContractTransaction(functionName, accountEntry, args);
        SimulateTransactionResult simulateTransactionResult = await SimulateTransactionAsync(new SimulateTransactionParams()
        {
            Transaction = EncodeTransaction(invokeContractTransaction),
        });
        Transaction assembledTransaction = simulateTransactionResult.ApplyTo(invokeContractTransaction);
        string encodedSignedTransaction = SignAndEncodeTransaction(assembledTransaction);
        SendTransactionResult sendTransactionResult = await SendTransactionAsync(new SendTransactionParams()
        {
            Transaction = encodedSignedTransaction,
        });
        return sendTransactionResult;
    }
    

    async Task<AccountEntry> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account)
    {
        GetLedgerEntriesResult getLedgerEntriesResult = await GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] {EncodedAccountKey(userAccount)},
        });
        LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
        return entry?.account;
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
                                contractId = new Hash(StrKey.DecodeContractId(contractId)),
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
        int max_attempts = 10;
        int attempts = 0;
        await AsyncDelay.Delay(delayMS);
        while (attempts < max_attempts)
        {
            Debug.Log("WaitForTransaction started");
            GetTransactionResult completion = await GetTransactionAsync(new GetTransactionParams()
            {
                Hash = txHash
            });
            switch (completion.Status)
            {
                case GetTransactionResultStatus.FAILED:
                    return null;
                case GetTransactionResultStatus.NOT_FOUND:
                    Debug.Log("Wait for transaction waiting a bit");
                    await AsyncDelay.Delay(delayMS);
                    continue;
                case GetTransactionResultStatus.SUCCESS:
                    return completion;
            }
        }

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
    
    async Task<string> SendJsonRequest(string json)
    {
        UnityWebRequest request = new UnityWebRequest(networkUri, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Debug.Log("SendJsonRequest sending off");
        await request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            Debug.Log("Response: " + request.downloadHandler.text);
            string content = request.downloadHandler.text;
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
}

public static class AsyncDelay
{
    public static Task Delay(int millisecondsDelay)
    {
#if UNITY_WEBGL && UNITY_EDITOR
        // Use a coroutine-based delay in WebGL
        return WaitForSecondsAsync(millisecondsDelay / 1000f);
#else
        return Task.Delay(millisecondsDelay);
#endif
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