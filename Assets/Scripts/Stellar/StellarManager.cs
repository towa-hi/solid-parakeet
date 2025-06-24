using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public static class StellarManager
{
    public static StellarDotnet stellar;
    
    public static string testContract = "CCCHAOQ6EZOPUJZWF2RNLTKXJBHBLZJLGL6V66PZIQO27K43MQNV5UYZ";
    public static AccountAddress testGuest = "GC7UFDAGZJMCKENUQ22PHBT6Y4YM2IGLZUAVKSBVQSONRQJEYX46RUAD";
    public static AccountAddress testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";
    public static string testGuestSneed = "SBHR4URT5RHIK4U4N45ZNUNEKLYEJYVFQSLSTR4A4RVNFHLIERGVZSIE";
    public static event Action OnNetworkStateUpdated;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    
    public static NetworkState networkState;

    static TaskInfo currentTask;

    public static void Initialize()
    {
        currentTask = null;
        networkState = new NetworkState();
        stellar = new StellarDotnet(testHostSneed, testContract);
    }

    public static async Task<bool> UpdateState()
    {
        TaskInfo getNetworkStateTask = SetCurrentTask("ReqNetworkState");
        networkState = await stellar.ReqNetworkState();
        EndTask(getNetworkStateTask);
        if (networkState.CurrentLobbyOutdated())
        {
            Debug.LogWarning($"UpdateState(): user.current_lobby is set to {networkState.user?.current_lobby} but unretrievable");
            await LeaveLobbyRequest();
            TaskInfo getNetworkStateTask2 = SetCurrentTask("ReqNetworkState2");
            networkState = await stellar.ReqNetworkState();
            EndTask(getNetworkStateTask2);
        }
        OnNetworkStateUpdated?.Invoke();
        return true;
    }
    
    public static async Task<bool> SetContractAddress(string contractId)
    {
        stellar.SetContractId(contractId);
        Debug.Log("OnContractAddressUpdated");
        await UpdateState();
        return true;
    }

    public static async Task<bool> SetSneed(string accountSneed)
    {
        stellar.SetSneed(accountSneed);
        Debug.Log("OnSneedUpdated");
        await UpdateState();
        return true;
    }

    public static string GetUserAddress()
    {
        return stellar.sneed != null ? stellar.userAddress : null;
    }

    public static string GetContractAddress()
    {
        return stellar.contractAddress;
    }

    static int ProcessTransactionResult(GetTransactionResult result, SimulateTransactionResult simResult)
    {
        if (simResult == null)
        {
            return -1;
        }
        if (simResult.Error != null)
        {
            List<int> errorCodes = new();
            foreach (DiagnosticEvent diag in simResult.DiagnosticEvents.Where(diag => !diag.inSuccessfulContractCall))
            {
                Debug.LogError(DiagnosticEventXdr.EncodeToBase64(diag)); 
                Debug.LogError(JsonUtility.ToJson(diag));
                ContractEvent.bodyUnion.case_0 body = (ContractEvent.bodyUnion.case_0)diag._event.body;
                foreach (SCVal topic in body.v0.topics)
                {
                    if (topic is not SCVal.ScvError { error: SCError.SceContract contractError }) continue;
                    int code = (int)contractError.contractCode.InnerValue;
                    errorCodes.Add(code);
                }
            }
            switch (errorCodes.Count)
            {
                case > 1:
                    Debug.LogWarning("ProcessTransactionResult failed to simulate with more than 1 error");
                    return errorCodes[0];
                case 1:
                    return errorCodes[0];
                default:
                    return -2;
            }
        }
        if (result == null)
        {
            return -3;
        }
        if (result.Status != GetTransactionResultStatus.SUCCESS)
        {
            return -4;
        }
        return 0;
    }

    public static async Task<int> MakeLobbyRequest(LobbyParameters parameters)
    {
        MakeLobbyReq req = new()
        {
            lobby_id = GenerateRandomUint(),
            parameters = parameters,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> LeaveLobbyRequest()
    {
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("leave_lobby", null);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> JoinLobbyRequest(LobbyId lobbyId)
    {
        JoinLobbyReq req = new()
        {
            lobby_id = lobbyId,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("join_lobby", req);
        EndTask(task);
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<int> CommitSetupRequest(LobbyId lobbyId, byte[] setupHash)
    {
        CommitSetupReq req = new()
        {
            lobby_id = lobbyId,
            setup_hash = setupHash,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("commit_setup", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> ProveSetupRequest(LobbyId lobbyId, Setup setup)
    {
        ProveSetupReq req = new()
        {
            lobby_id = lobbyId,
            setup = setup,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_setup", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> CommitMoveRequest(LobbyId lobbyId, byte[] hiddenMoveHash)
    {
        CommitMoveReq req = new()
        {
            lobby_id = lobbyId,
            move_hash = hiddenMoveHash,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("commit_move", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> ProveMoveRequest(LobbyId lobbyId, HiddenMove hiddenMove)
    {
        ProveMoveReq req = new()
        {
            lobby_id = lobbyId,
            move_proof = hiddenMove,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_move", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> ProveRankRequest(LobbyId lobbyId, HiddenRank[] hiddenRanks)
    {
        ProveRankReq req = new()
        {
            lobby_id = lobbyId,
            hidden_ranks = hiddenRanks,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_rank", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<AccountEntry> GetAccount(string key)
    {
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        AccountEntry result = await stellar.ReqAccountEntry(MuxedAccount.FromAccountId(key));
        EndTask(task);
        return result;
    }

    public static async Task<TrustLineEntry> GetAssets(string userId)
    {
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        MuxedAccount.KeyTypeEd25519 userAccount = MuxedAccount.FromAccountId(userId);

        LedgerEntry.dataUnion.Trustline result = await stellar.GetAssets(userAccount);
        EndTask(task);
        OnAssetsUpdated?.Invoke(result.trustLine);
        return result.trustLine;
    }
    
    static TaskInfo SetCurrentTask(string message)
    {
        if (currentTask != null)
        {
            throw new Exception("Task is already set");
        }
        TaskInfo taskInfo = new();
        taskInfo.taskId = Guid.NewGuid();
        taskInfo.taskMessage = message;
        currentTask = taskInfo;
        OnTaskStarted?.Invoke(taskInfo);
        return taskInfo;
    }

    static void EndTask(TaskInfo taskInfo)
    {
        if (taskInfo == null)
        {
            throw new Exception("Task is null");
        }
        if (currentTask == null)
        {
            throw new Exception("Task is not set");
        }
        if (currentTask.taskId != taskInfo.taskId)
        {
            throw new Exception("Task is not taskId");
        }
        OnTaskEnded?.Invoke(currentTask);
        currentTask = null;
    }
    
    static uint GenerateRandomUint()
    {
        uint value;
        do
        {
            // range is [0, UInt32.MaxValue]
            value = (uint)Random.Range(1, int.MaxValue)      // first half
                    << 16
                    | (uint)Random.Range(0, ushort.MaxValue); // second half
        } while (value == 0);
        return value;
    }
}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}