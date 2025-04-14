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

public class StellarManagerTest
{
    public static StellarDotnet stellar;
    
    public static string testContract = "CBRKQJMVTJ3SGKN32GQG3PEWIRUQVN3LQZEYOCK3D6GZ4O3PRXQGWAOD";
    public static string testGuest = "GD6APTUYGQJUR2Q5QQGKCZNBWE7BVLTZQAJ4LRX5ZU55ODB65FMJBGGO";
    //public static string testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";
    public static string testGuestSneed = "SBHR4URT5RHIK4U4N45ZNUNEKLYEJYVFQSLSTR4A4RVNFHLIERGVZSIE";
    public static event Action<string> OnSneedUpdated;
    public static event Action<string> OnContractAddressUpdated;
    public static event Action<User?> OnCurrentUserUpdated;
    public static event Action<Lobby?> OnCurrentLobbyUpdated;
    
    public static User? currentUser = null;
    public static Lobby? currentLobby = null;
    
    public static void Initialize()
    {
        stellar = new StellarDotnet(testHostSneed, testContract);
        Debug.Log("Initialized sneed and contract address");
        Debug.Log("sneed" + stellar.sneed);
        Debug.Log("contract" + stellar.contractAddress);
    }

    public static async Task<bool> UpdateState()
    {
        User? oldUser = currentUser;
        Lobby? oldLobby = currentLobby;
        Debug.Log("Updating State...");
        Debug.Log("sneed " + stellar.sneed);
        Debug.Log("contract " + stellar.contractAddress);
        currentUser = null;
        currentLobby = null;
        if (stellar.sneed == null || stellar.contractAddress == null)
        {
            Debug.Log("no sneed or contract address");
            return true;
        }
        currentUser = await GetUser(stellar.userAddress);
        if (currentUser.HasValue)
        {
            Debug.Log("currentUser set to " + currentUser.Value.index);
        }
        else
        {
            Debug.Log("currentUser set to null");
            Debug.Log("currentLobby set to null because current user is null");
        }
        if (currentUser.HasValue)
        {
            if (!string.IsNullOrEmpty(currentUser.Value.current_lobby))
            {
                currentLobby = await GetLobby(currentUser.Value.current_lobby);
                if (currentLobby.HasValue)
                {
                    Debug.Log("currentLobby set to " + currentLobby.Value.index);
                }
                else
                {
                    Debug.Log("currentLobby set to null because lobby could not be fetched");
                }
            }
            else
            {
                Debug.Log("currentLobby set to null because currentUser.current_lobby is empty");
            }
        }
        Debug.Log("OnCurrentUserUpdated");
        OnCurrentUserUpdated?.Invoke(currentUser);
        Debug.Log("OnCurrentLobbyUpdated");
        OnCurrentLobbyUpdated?.Invoke(currentLobby);
        return true;
    }
    
    public static async Task SetContractAddress(string contractId)
    {
        
        stellar.SetContractId(contractId);
        Debug.Log("OnContractAddressUpdated");
        OnContractAddressUpdated?.Invoke(contractId);
        await UpdateState();
    }

    public static async Task SetSneed(string accountSneed)
    {
        stellar.SetSneed(accountSneed);
        Debug.Log("OnSneedUpdated");
        OnSneedUpdated?.Invoke(accountSneed);
        await UpdateState();
    }

    public static string GetUserAddress()
    {
        return stellar.sneed != null ? stellar.userAddress : null;
    }

    public static string GetContractAddress()
    {
        return stellar.contractAddress;
    }
    public static async Task<int> MakeLobbyRequest(Contract.LobbyParameters parameters)
    {
        uint salt = (uint)Random.Range(0, 4000000);
        MakeLobbyReq req = new MakeLobbyReq
        {
            host_address = GetUserAddress(),
            parameters = parameters,
            salt = salt,
        };
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req);
        if (simResult == null)
        {
            Debug.LogError("MakeLobbyReq simResult is null");
            return -1;
        }
        else if (simResult.Error != null)
        {
            Debug.Log("MakeLobbyReq sim got " + simResult.Error);
            List<int> errorCodes = GetErrorCodes(simResult.DiagnosticEvents);
            if (errorCodes.Count > 1)
            {
                Debug.LogWarning("MakeLobbyRequest failed to simulate with more than 1 error");
            }
            if (errorCodes.Count == 1)
            {
                return errorCodes[0];
            }
            else
            {
                return -666;
            }
        }
        else if (result == null)
        {
            Debug.LogError("MakeLobbyReq final result is null");
            return -2;
        }
        else if (result.Status != GetTransactionResultStatus.SUCCESS)
        {
            Debug.LogWarning("MakeLobbyReq sim got " + result.Status);
            return -3;
        }
        return 0;
    }

    public static async Task<int> LeaveLobbyRequest()
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

    public static async Task<int> JoinLobbyRequest(string lobbyId)
    {
        JoinLobbyReq req = new()
        {
            guest_address = GetUserAddress(),
            lobby_id = lobbyId,
        };
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("join_lobby", req);
        if (simResult == null)
        {
            Debug.LogError("JoinLobbyRequest simResult is null");
            return -1;
        }
        else if (simResult.Error != null)
        {
            Debug.Log("JoinLobbyRequest sim got " + simResult.Error);
            List<int> errorCodes = GetErrorCodes(simResult.DiagnosticEvents);
            if (errorCodes.Count > 1)
            {
                Debug.LogWarning("JoinLobbyRequest failed to simulate with more than 1 error");
            }
            if (errorCodes.Count == 1)
            {
                return errorCodes[0];
            }
            else
            {
                return -666;
            }
        }
        else if (result == null)
        {
            Debug.LogError("JoinLobbyRequest final result is null");
            return -2;
        }
        else if (result.Status != GetTransactionResultStatus.SUCCESS)
        {
            Debug.LogWarning("JoinLobbyRequest sim got " + result.Status);
            return -3;
        }
        return 0;
    }
    
    static List<int> GetErrorCodes(List<DiagnosticEvent> diagnosticEvents)
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

    public static async Task<Lobby?> GetLobby(string key)
    {
        Lobby? result = await stellar.ReqLobbyData(key);
        return result;
    }

    public static async Task<User?> GetUser(string key)
    {
        User? result = await stellar.ReqUserData(key);
        return result;
    }

}
