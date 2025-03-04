using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;

public class StellarDotnet : MonoBehaviour
{
    public string contractId;
    MuxedAccount.KeyTypeEd25519 userAccount;
    public StellarRPCClient client;
    
    public StellarDotnet(string inSecretSneed, string inContractId)
    {
        HttpClient httpClient = new();
        httpClient.BaseAddress = new Uri("https://soroban-testnet.stellar.org");
        client = new StellarRPCClient(httpClient);
        Network.UseTestNetwork();
        SetUserAccount(inSecretSneed);
        SetContractId(inContractId);
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


