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

public static class StellarManagerTest
{
    public static StellarDotnet stellar;
    
    public static string testContract = "CDZTF76LQVE3W5TXXEW3Q6AC57AKFBZMBAUL3TRMXOH2675QV3RR6EPE";
    public static StellarAccountAddress testGuest = "GC7UFDAGZJMCKENUQ22PHBT6Y4YM2IGLZUAVKSBVQSONRQJEYX46RUAD";
    public static StellarAccountAddress testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";
    public static string testGuestSneed = "SBHR4URT5RHIK4U4N45ZNUNEKLYEJYVFQSLSTR4A4RVNFHLIERGVZSIE";
    public static event Action OnNetworkStateUpdated;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    public static TaskInfo currentTask;
    
    public static User? currentUser;
    public static Lobby? currentLobby;
    
    public static void Initialize()
    {
        currentTask = null;
        currentUser = null;
        currentLobby = null;
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
        currentUser = await stellar.ReqUserData(stellar.userAccountAddress);
        EndTask(getUserTask);
        if (currentUser.HasValue)
        {
            if (currentUser.Value.current_lobby != 0)
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
    
    public static async Task<int> MakeLobbyRequest(Contract.LobbyParameters parameters)
    {
        MakeLobbyReq req = new MakeLobbyReq
        {
            lobby_id = GenerateRandomUint(),
            parameters = parameters,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req);
        EndTask(task);
        //await UpdateState();
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

    public static async Task<int> SubmitMoveHash()
    {
        // silently update lobby
        Assert.IsTrue(currentUser.HasValue);
        TaskInfo getLobbyTask = SetCurrentTask("ReqLobbyData");
        currentLobby = await stellar.ReqLobbyData(currentUser.Value.current_lobby);
        EndTask(getLobbyTask);
        Assert.IsTrue(currentLobby.HasValue);
        MoveResolveReq req = Globals.ResolveTurn(currentLobby.Value, currentUser.Value.index);
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("resolve_move", req);
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

    public static async Task<int> SendMail(Mail mail)
    {
        Assert.IsTrue(currentLobby.HasValue);
        SendMailReq req = new()
        {
            lobby = currentLobby.Value.index,
            mail = mail,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("send_mail", req);
        EndTask(task);
        return ProcessTransactionResult(result, simResult);
    }

    public static async Task<Mailbox?> GetMail(string lobbyId)
    {
        Mailbox? result = await stellar.ReqMailData(lobbyId);
        return result;
    }
    
    public static async Task<Lobby?> GetLobby(uint key)
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

    public static async Task<TrustLineEntry> GetAssets(string userId)
    {
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        MuxedAccount.KeyTypeEd25519 userAccount = MuxedAccount.FromAccountId(userId);

        LedgerEntry.dataUnion.Trustline result = await stellar.GetAssets(userAccount);
        EndTask(task);
        OnAssetsUpdated?.Invoke(result.trustLine);
        return result.trustLine;
    }
}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}