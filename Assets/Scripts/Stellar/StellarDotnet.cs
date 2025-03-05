using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ContractTypes;
using Newtonsoft.Json;
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
    
    public async Task<bool> TestFunction()
    {
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        // make structs
        NestedTestReq nestedTestReq = new()
        {
            number = 34,
            word = "nested word",
            flat = new FlatTestReq
            {
                number = 21,
                word = "flat word",
            },
        };
        SCVal.ScvAddress addressArg = new SCVal.ScvAddress
        {
            address = new SCAddress.ScAddressTypeAccount()
            {
                accountId = accountId,
            },
        };
        SCVal arg = SCValConverter.NativeToSCVal(nestedTestReq);
        SCVal[] args = {addressArg, arg };
        SendTransactionResult result = await InvokeContractFunction(accountEntry, "nested_param_test", args);
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
        return new SendTransactionResult();
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

    async Task<GetTransactionResult> WaitForTransaction(SendTransactionResult result)
    {
        //yield return new WaitForSeconds(3);
        // TODO: finish this
        return new GetTransactionResult();
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
        JsonRpcResponse<SimulateTransactionResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<SimulateTransactionResult>>(content);
        SimulateTransactionResult transactionResult = rpcResponse.Error == null ? rpcResponse.Result : throw new JsonRpcException(rpcResponse.Error);
        return transactionResult;
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
            JsonRpcResponse<GetLatestLedgerResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLatestLedgerResult>>(content, _jsonSettings);
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
