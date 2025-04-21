using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public class StellarManagerTest
{
    public static StellarDotnet stellar;
    
    public static string testContract = "CBI5SRQT5435IQTT2HL3CDPSFESRUXLHFUEOKCTY2RLXTI7ZASTCARPC";
    public static string testGuest = "GD6APTUYGQJUR2Q5QQGKCZNBWE7BVLTZQAJ4LRX5ZU55ODB65FMJBGGO";
    //public static string testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";
    public static string testGuestSneed = "SBHR4URT5RHIK4U4N45ZNUNEKLYEJYVFQSLSTR4A4RVNFHLIERGVZSIE";
    public static event Action OnNetworkStateUpdated;
    
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    public static TaskInfo currentTask;
    
    public static User? currentUser = null;
    public static Lobby? currentLobby = null;
    
    public static void Initialize()
    {
        stellar = new StellarDotnet(testHostSneed, testContract);
        Debug.Log("Initialized sneed and contract address");
        Debug.Log("sneed" + stellar.sneed);
        Debug.Log("contract" + stellar.contractAddress);
    }

    static TaskInfo SetCurrentTask(string message)
    {
        if (currentTask != null)
        {
            throw new Exception("Task is already set");
        }
        TaskInfo taskInfo = new TaskInfo();
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
    
    public static async Task<bool> UpdateState()
    {
        currentUser = null;
        currentLobby = null;
        TaskInfo getUserTask = SetCurrentTask("ReqUserData");
        currentUser = await stellar.ReqUserData(stellar.userAddress);
        EndTask(getUserTask);
        if (currentUser.HasValue)
        {
            if (!string.IsNullOrEmpty(currentUser.Value.current_lobby))
            {
                TaskInfo getLobbyTask = SetCurrentTask("ReqLobbyData");
                currentLobby = await stellar.ReqLobbyData(currentUser.Value.current_lobby);
                EndTask(getLobbyTask);
            }
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
                Debug.Log(diag); 
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
    
    public static async Task<int> MakeLobbyRequest(Contract.LobbyParameters parameters)
    {
        
        uint salt = (uint)Random.Range(0, 4000000);
        MakeLobbyReq req = new MakeLobbyReq
        {
            host_address = GetUserAddress(),
            parameters = parameters,
            salt = salt,
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

    public static async Task<int> CommitSetupRequest(Dictionary<string, PawnCommitment> commitments)
    {
        Assert.IsTrue(currentLobby.HasValue);
        
        PawnCommitment[] setupCommitments = new PawnCommitment[commitments.Count];
        for (int i = 0; i < commitments.Count; i++)
        {
            KeyValuePair<string, PawnCommitment> kvp = commitments.ElementAt(i);
            PawnCommitment commitment = kvp.Value;
            setupCommitments[i] = commitment;
        }
        SetupCommitReq req = new()
        {
            lobby_id = currentLobby.Value.index,
            setup_commitments = setupCommitments,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("commit_setup", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<int> QueueMove(QueuedMove queuedMove)
    {
        Assert.IsTrue(currentLobby.HasValue);
        Assert.IsTrue(currentUser.HasValue);
        Lobby lobby = currentLobby.Value;
        User user = currentUser.Value;
        Assert.IsNotNull(queuedMove);
        Turn currentTurn = lobby.turns.Last();
        bool isHost = false || currentTurn.host_turn.user_address == user.index;
        TurnMove turnMove = isHost ? currentTurn.host_turn : currentTurn.guest_turn;
        if (turnMove.initialized)
        {
            Debug.LogError("user already moved");
            return -666;
        }
        MoveSubmitReq req = new()
        {
            lobby = lobby.index,
            move_pos = new Pos(queuedMove.pos),
            pawn_id = queuedMove.pawnId.ToString(),
            turn = turnMove.turn,
            user_address = turnMove.user_address,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("submit_move", req);
        EndTask(task);
        await UpdateState();
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<int> JoinLobbyRequest(string lobbyId)
    {
        JoinLobbyReq req = new()
        {
            guest_address = GetUserAddress(),
            lobby_id = lobbyId,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("join_lobby", req);
        EndTask(task);
        return ProcessTransactionResult(result, simResult);
    }
    
    public static async Task<Lobby?> GetLobby(string key)
    {
        TaskInfo task = SetCurrentTask("ReqLobbyData");
        Lobby? result = await stellar.ReqLobbyData(key);
        EndTask(task);
        return result;
    }

    public static async Task<AccountEntry> GetAccount(string key)
    {
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        AccountEntry result = await stellar.ReqAccountEntry(MuxedAccount.FromAccountId(key));
        EndTask(task);
        return result;
    }

    
}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}