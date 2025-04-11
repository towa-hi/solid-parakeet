using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

public class StellarManagerTest : MonoBehaviour
{
    public static string test;
    public static StellarDotnet stellar;
    
    public static string testContract = "CDVIDU4LVBQRG6VIHGYM7TGRNX3F2WY6RXV5UG45VG7L37NPG4CGKFCJ";
    public static string testGuest = "GD6APTUYGQJUR2Q5QQGKCZNBWE7BVLTZQAJ4LRX5ZU55ODB65FMJBGGO";
    public static string testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";

    public static event Action<string> OnContractIdChanged;
    public static event Action<string> OnAccountIdChanged;
    
    void Awake()
    {
        stellar = new StellarDotnet(testHostSneed, testContract);
    }
    
    public void Initialize()
    {
        
    }

    public static void SetContractId(string contractId)
    {
        stellar.SetContractId(contractId);
        OnContractIdChanged?.Invoke(contractId);
    }

    public static void SetAccountId(string accountSneed)
    {
        stellar.SetAccountId(accountSneed);
        OnAccountIdChanged?.Invoke(accountSneed);
    }

    public async Task<(int, Lobby?)> MakeLobbyRequest(Contract.LobbyParameters parameters)
    {
        uint salt = (uint)Random.Range(0, 10000000);
        MakeLobbyReq req = new MakeLobbyReq
        {
            host_address = StrKey.EncodeStellarAccountId(stellar.userAccount.PublicKey),
            parameters = parameters,
            salt = salt,
        };
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req);
        if (simResult.Error != null)
        {
            Debug.Log(simResult.Error);
            List<int> errorCodes = GetErrorCodes(simResult.DiagnosticEvents);
            if (errorCodes.Count > 1)
            {
                Debug.LogWarning("MakeLobbyRequest failed to simulate with more than 1 error");
            }
            if (errorCodes.Count == 1)
            {
                return (errorCodes[0], null);
            }
        }
        SCVal returnValue = (result.TransactionResultMeta as TransactionMeta.case_3).v3.sorobanMeta.returnValue;
        string lobbyId = SCUtility.SCValToNative<string>(returnValue);
        Lobby? lobby = await GetLobby(lobbyId);
        return (0, lobby);
    }

    public async Task<int> LeaveLobbyRequest()
    {
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("leave_lobby", null);
        if (simResult.Error != null)
        {
            Debug.Log(simResult.Error);
            List<int> errorCodes = GetErrorCodes(simResult.DiagnosticEvents);
            if (errorCodes.Count > 1)
            {
                Debug.LogWarning("LeaveLobbyRequest failed to simulate with more than 1 error");
            }
            if (errorCodes.Count == 1)
            {
                return errorCodes[0];
            }
        }
        return 0;
    }
    
    List<int> GetErrorCodes(List<DiagnosticEvent> diagnosticEvents)
    {
        List<int> errorCodes = new List<int>();
        foreach (DiagnosticEvent diag in diagnosticEvents.Where(diag => !diag.inSuccessfulContractCall))
        {
            Debug.Log(diag); 
            ContractEvent.bodyUnion.case_0 body = (ContractEvent.bodyUnion.case_0)diag._event.body;
            foreach (SCVal topic in body.v0.topics)
            {
                if (topic is SCVal.ScvError { error: SCError.SceContract contractError })
                {
                    int code = (int)contractError.contractCode.InnerValue;
                    errorCodes.Add(code);
                }
            }
        }
        return errorCodes;
    }

    public async Task<Lobby?> GetLobby(string key)
    {
        Lobby? result = await stellar.ReqLobby(key);
        return result;
    }
}
