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
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public static class StellarManager
{
    public static StellarDotnet stellar;
    
    public static string testContract = "CA5RJCPHPL4VO75IEQZFJKVDJF27IRSY5NYNJRF6ZTWKJN6FFFAEJ64Y";
    public static AccountAddress testHost = "GBAYHJ6GFSXZV5CXQWGNRZ2NU3QR6OBW4RYIHL6EB4IEPYC7JPRVZDR3";
    public static AccountAddress testGuest = "GAOWUE62RVIIDPDFEF4ZOAHECXVEBJJNR66F6TG7F4PWQATZKRNZC53S";
    public static string testHostSneed = "SA25YDMQQ5DSGVSJFEEGNJEMRMITRAA6PQUVRRLDRFUN5GMMBPFVLDVM";
    public static string testGuestSneed = "SD43VTCJENK36DTZD5BJTTHVCWU3ZYYD342S247UE6MK57Y7BABMZVPU";
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
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("UpdateState");
        TaskInfo getNetworkStateTask = SetCurrentTask("ReqNetworkState");
        networkState = await stellar.ReqNetworkState(tracker);
        EndTask(getNetworkStateTask);
        if (networkState.user is User user && user.current_lobby != 0)
        {
            if (!networkState.lobbyInfo.HasValue || !networkState.lobbyParameters.HasValue)
            {
                Debug.LogWarning($"UpdateState(): user.current_lobby is set to {networkState.user?.current_lobby} but lobby data is missing - likely expired");
                User updatedUser = user;
                updatedUser.current_lobby = new LobbyId(0);
                networkState.user = updatedUser;
                Debug.Log("Set current_lobby to null due to expired/missing lobby");
            }
        }
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
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
        if (result.Status != GetTransactionResult_Status.SUCCESS)
        {
            return -4;
        }
        return 0;
    }

    public static async Task<int> MakeLobbyRequest(LobbyParameters parameters)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"MakeLobbyRequest");
        MakeLobbyReq req = new()
        {
            lobby_id = GenerateLobbyId(),
            parameters = parameters,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> LeaveLobbyRequest()
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"LeaveLobbyRequest");
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("leave_lobby", null, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<int> JoinLobbyRequest(LobbyId lobbyId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"JoinLobbyRequest");
        JoinLobbyReq req = new()
        {
            lobby_id = lobbyId,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("join_lobby", req, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<int> CommitSetupRequest(LobbyId lobbyId, byte[] root, Setup setup, List<CachedRankProof> cached)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"CommitSetupRequest");
        CommitSetupReq req = new()
        {
            lobby_id = lobbyId,
            rank_commitment_root = root,
            setup = setup,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult getResult, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("commit_setup", req, tracker);
        EndTask(task);
        tracker.EndOperation();
        if (getResult == null)
        {
            Debug.LogError("CallVoidFunction: timed out or failed to connect");
        }
        else if (getResult.Status != GetTransactionResult_Status.SUCCESS)
        {
            Debug.LogWarning($"CallVoidFunction: status: {getResult.Status}");
        }
        else
        {
            CacheManager.StoreHiddenRanksAndProofs(cached, networkState.address, lobbyId);
        }
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return ProcessTransactionResult(getResult, simResult);
    }

    public static async Task<int> CommitMoveRequest(LobbyId lobbyId, byte[] hiddenMoveHash, HiddenMove hiddenMove)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("CommitMoveRequest");
        CommitMoveReq commitMoveReq = new()
        {
            lobby_id = lobbyId,
            move_hash = hiddenMoveHash,
        };
        ProveMoveReq proveMoveReq = new()
        {
            lobby_id = lobbyId,
            move_proof = hiddenMove,
        };
        // NOTE: voodoo to decide if we can get away with sending proveMoveReq in the same transaction
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        AccountEntry accountEntry = await stellar.ReqAccountEntry(stellar.userAccount, tracker);
        (Transaction invokeContractTransaction, SimulateTransactionResult simResult) = await stellar.SimulateFunction(accountEntry, "commit_move", commitMoveReq, tracker);
        if (simResult.Error != null)
        {
            EndTask(task);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            return ProcessTransactionResult(null, simResult);
        }
        SCVal scVal = simResult.Results.FirstOrDefault()!.Result;
        LobbyInfo simulatedLobbyInfo = SCUtility.SCValToNative<LobbyInfo>(scVal);
        bool isHost = simulatedLobbyInfo.IsHost(stellar.userAddress);
        Subphase mySubphase = isHost ? Subphase.Host : Subphase.Guest;
        bool sendProveMove = simulatedLobbyInfo.phase == Phase.MoveProve &&
                             (simulatedLobbyInfo.subphase == Subphase.Both || simulatedLobbyInfo.subphase == mySubphase);
        if (sendProveMove)
        {
            // it sucks but we have to get accountentry again or the tx seq wont be right 
            AccountEntry accountEntry2 = await stellar.ReqAccountEntry(stellar.userAccount, tracker);
            (GetTransactionResult combinedTransactionResult, SimulateTransactionResult combinedSimResult) = await stellar.CallVoidFunctionWithTwoParameters(accountEntry2,"commit_move_and_prove_move", commitMoveReq, proveMoveReq, tracker);
            if (combinedTransactionResult == null)
            {
                Debug.LogError("CallVoidFunction: timed out or failed to connect");
            }
            else if (combinedTransactionResult.Status != GetTransactionResult_Status.SUCCESS)
            {
                Debug.LogWarning($"CallVoidFunction: status: {combinedTransactionResult.Status}");
            }
            EndTask(task);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return ProcessTransactionResult(combinedTransactionResult, combinedSimResult);
        }
        else
        {
            GetTransactionResult getResult = await stellar.CallVoidFunctionWithoutSimulating(invokeContractTransaction, simResult, tracker);
            if (getResult == null)
            {
                Debug.LogError("CallVoidFunction: timed out or failed to connect");
            }
            else if (getResult.Status != GetTransactionResult_Status.SUCCESS)
            {
                Debug.LogWarning($"CallVoidFunction: status: {getResult.Status}");
            }
            else
            {
                CacheManager.StoreHiddenMove(hiddenMove, networkState.address, lobbyId);
            }
            EndTask(task);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return ProcessTransactionResult(getResult, simResult);
        }
        
        
        
    }

    public static async Task<int> ProveMoveRequest(LobbyId lobbyId, HiddenMove hiddenMove)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveMoveRequest");
        ProveMoveReq req = new()
        {
            lobby_id = lobbyId,
            move_proof = hiddenMove,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_move", req, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> ProveRankRequest(LobbyId lobbyId, HiddenRank[] hiddenRanks, MerkleProof[] merkleProofs)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveRankRequest");
        ProveRankReq req = new()
        {
            lobby_id = lobbyId,
            hidden_ranks = hiddenRanks,
            merkle_proofs = merkleProofs,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_rank", req, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<AccountEntry> GetAccount(string key)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"GetAccount");
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        AccountEntry result = await stellar.ReqAccountEntry(MuxedAccount.FromAccountId(key));
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        return result;
    }

    public static async Task<TrustLineEntry> GetAssets(string userId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"GetAssets");
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        MuxedAccount.KeyTypeEd25519 userAccount = MuxedAccount.FromAccountId(userId);

        LedgerEntry.dataUnion.Trustline result = await stellar.GetAssets(userAccount, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
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
    
    // NOTE: lobby IDs will be done server side in the future
    static LobbyId GenerateLobbyId()
    {
        uint value = (uint)Random.Range(100000, 1000000); // 6 digit number
        return new LobbyId(value);
    }
}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}