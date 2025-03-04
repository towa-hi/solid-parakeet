using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

public class StellarDotnet : MonoBehaviour
{
    public string contractId;
    MuxedAccount.KeyTypeEd25519 userAccount;
    public StellarRPCClient client;
    JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };
    
    public StellarDotnet(string inSecretSneed, string inContractId)
    {
        HttpClient httpClient = new();
        httpClient.BaseAddress = new Uri("https://soroban-testnet.stellar.org");
        client = new StellarRPCClient(httpClient);
        Network.UseTestNetwork();
        SetUserAccount(inSecretSneed);
        SetContractId(inContractId);
    }
    public async Task<bool> TestFunction()
    {
        AccountID accountId = new AccountID(userAccount.XdrPublicKey);
        LedgerKey accountKey = new LedgerKey.Account()
        {
            account = new LedgerKey.accountStruct()
            {
                accountID = accountId,
            },
        };
        string encodedAccountKey = LedgerKeyXdr.EncodeToBase64(accountKey);
        GetLedgerEntriesParams getLedgerEntriesArgs = new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedAccountKey},
        };
        Debug.Log(getLedgerEntriesArgs.Keys.Count);
        Debug.Log(encodedAccountKey);
        JsonRpcRequest request = new()
        {
            JsonRpc = "2.0",
            Method = "getLedgerEntries",
            Params = (object) getLedgerEntriesArgs,
            Id = 1,
        };
        string requestJson = JsonConvert.SerializeObject((object) request, jsonSettings);
        Debug.Log(requestJson);
        string response = await SendJsonRequest("https://soroban-testnet.stellar.org", requestJson);
        Debug.Log(response);
        return true;
    }
    
    async Task<string> SendJsonRequest(string url, string json)
    {
        UnityWebRequest request = new UnityWebRequest(url, "POST");
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
            JsonRpcResponse<GetLatestLedgerResult> rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<GetLatestLedgerResult>>(content, jsonSettings);
        }
        return request.downloadHandler.text;
    }
    
    public void SetContractId(string inContractId)
    {
        contractId = inContractId;
    }

    public void SetUserAccount(string inSecretSneed)
    {
        userAccount = MuxedAccount.FromSecretSeed(inSecretSneed);
    }

    public async Task<SendTransactionResult> InvokeContractFunction(string functionName, SCVal[] args)
    {
        AccountEntry accountEntry = await ReqAccountEntry(userAccount);
        return new SendTransactionResult();
    }

    async Task<AccountEntry> ReqAccountEntry(MuxedAccount.KeyTypeEd25519 account)
    {
        string accountKeyEncoded = CreateEncodedAccountKey(account);
        GetLedgerEntriesResult getLedgerEntriesResult = await client.GetLedgerEntriesAsync(new GetLedgerEntriesParams()
        {
            Keys = new [] { accountKeyEncoded },
        });
        LedgerEntry.dataUnion.Account entry = getLedgerEntriesResult.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
        return entry?.account;
    }

    static Operation CreateFunctionInvocationOperation(string contractId, MuxedAccount.KeyTypeEd25519 account, string functionName, SCVal[] args)
    {
        Operation operation = new Operation()
        {
            sourceAccount = account,
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
                            
                        }
                    }
                }
            },
        };
        return operation;
    }
    
    static string CreateEncodedAccountKey(MuxedAccount.KeyTypeEd25519 account)
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



[Preserve]
public class JsonRpcRequestPreserved
{
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonProperty("method")]
    public string Method { get; set; } = "";

    [JsonProperty("params")]
    public object Params { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }
}