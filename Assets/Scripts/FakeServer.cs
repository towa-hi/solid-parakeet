using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public class FakeServer : MonoBehaviour
{
    
    public Contract.LobbyParameters fakeParameters;
    public Contract.Lobby fakeLobby;
    public Contract.User fakeHost;
    public Contract.User fakeGuest;
    public string fakeLobbyId = Guid.Empty.ToString();

    public static FakeServer ins;
    public BoardDef boardDef;
    
    void Awake()
    {
        ins = this;
        fakeGuest = new User
        {
            current_lobby = fakeLobbyId,
            games_completed = 0,
            index = StellarManagerTest.testGuest,
            name = "guest",
        };
        fakeHost = new User
        {
            current_lobby = fakeLobbyId,
            games_completed = 0,
            index = StellarManagerTest.testHost,
            name = "host",
        };
    }

    public void SetFakeParameters(Contract.LobbyParameters parameters)
    {
        fakeParameters = parameters;
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        boardDef = boardDefs.FirstOrDefault(def => def.name == fakeParameters.board_def_name);
    }
    
    
    public void StartFakeLobby()
    {
        List<Contract.Pawn> pawnsList = new List<Contract.Pawn>();
        List<PawnCommitment> hostCommitments = new List<PawnCommitment>();
        List<PawnCommitment> guestCommitments = new List<PawnCommitment>();
        
        foreach (MaxPawns maxPawn in fakeParameters.max_pawns)
        {
            PawnDef def = Globals.RankToPawnDef((Rank)maxPawn.rank);
            for (int i = 0; i < maxPawn.max; i++)
            {
                Contract.Pawn pawn = new Contract.Pawn
                {
                    is_alive = false,
                    is_moved = false,
                    is_revealed = false,
                    pawn_def_hash = Globals.PawnDefToFakeHash(def),
                    pawn_id = Guid.NewGuid().ToString(),
                    pos = new Pos(Globals.Purgatory),
                    team = 1,
                };
                PawnCommitment commitment = new PawnCommitment
                {
                    pawn_def_hash = pawn.pawn_def_hash,
                    pawn_id = pawn.pawn_id,
                    starting_pos = pawn.pos,
                };
                pawnsList.Add(pawn);
                hostCommitments.Add(commitment);
            }
        }
        foreach (MaxPawns maxPawn in fakeParameters.max_pawns)
        {
            PawnDef def = Globals.RankToPawnDef((Rank)maxPawn.rank);
            for (int i = 0; i < maxPawn.max; i++)
            {
                Contract.Pawn pawn = new Contract.Pawn
                {
                    is_alive = false,
                    is_moved = false,
                    is_revealed = false,
                    pawn_def_hash = Globals.PawnDefToFakeHash(def),
                    pawn_id = Guid.NewGuid().ToString(),
                    pos = new Pos(Globals.Purgatory),
                    team = 2,
                };
                PawnCommitment commitment = new PawnCommitment
                {
                    pawn_def_hash = pawn.pawn_def_hash,
                    pawn_id = pawn.pawn_id,
                    starting_pos = pawn.pos,
                };
                pawnsList.Add(pawn);
                guestCommitments.Add(commitment);
            }
        }
        fakeLobby = new Lobby
        {
            game_end_state = 3,
            guest_address = fakeGuest.index,
            guest_state = new UserState
            {
                committed = false,
                lobby_state = 2,
                setup_commitments = guestCommitments.ToArray(),
                team = 2,
                user_address = fakeGuest.index,
            },
            host_address = fakeHost.index,
            host_state = new UserState
            {
                committed = false,
                lobby_state = 2,
                setup_commitments = hostCommitments.ToArray(),
                team = 1,
                user_address = fakeHost.index,
            },
            index = fakeLobbyId,
            parameters = fakeParameters,
            pawns = pawnsList.ToArray(),
            phase = 1,
            turns = new Turn[] {},
        };
        // fakeLobby is updated to the result of these functions
        // host -> make_lobby
        // guest -> join_lobby
    }

    
    public int CommitSetupRequest(Dictionary<string, PawnCommitment> commitments)
    {
        // Convert dictionary to array for the host's setup commitment request
        PawnCommitment[] setupCommitments = new PawnCommitment[commitments.Count];
        for (int i = 0; i < commitments.Count; i++)
        {
            KeyValuePair<string, PawnCommitment> kvp = commitments.ElementAt(i);
            PawnCommitment commitment = kvp.Value;
            setupCommitments[i] = commitment;
        }
        
        // Create the host's setup commit request
        SetupCommitReq hostReq = new()
        {
            lobby_id = fakeLobby.index,
            setup_commitments = setupCommitments,
        };
        // Generate and process the guest's request
        SetupCommitReq guestReq = GuestSetupCommitReq(fakeGuest, fakeLobby);
        int guestResult = CommitSetup(fakeGuest.index, guestReq);
        if (guestResult != 0)
        {
            Debug.LogError($"Guest commit failed with error code {guestResult}");
            return guestResult;
        }
        // Process the host's request
        int hostResult = CommitSetup(fakeHost.index, hostReq);
        if (hostResult != 0)
        {
            Debug.LogError($"Host commit failed with error code {hostResult}");
            return hostResult;
        }
        return 0; // Success
    }

    SetupCommitReq GuestSetupCommitReq(User guest, Lobby lobby)
    {
        // Get the guest's user state and commitments
        UserState guestState = lobby.guest_state;
        Dictionary<string, PawnCommitment> commitments = guestState.setup_commitments.ToDictionary(commitment => commitment.pawn_id);
        // We need to generate valid positions for each pawn commitment
        // Similar to SetupTestPhase.OnAutoSetup
        HashSet<Tile> usedTiles = new();
        foreach (MaxPawns maxPawns in lobby.parameters.max_pawns)
        {
            for (int i = 0; i < maxPawns.max; i++)
            {
                // Get available tiles for this rank
                List<Tile> availableTiles = boardDef.GetEmptySetupTiles(lobby.GetTeam(guest.index), (Rank)maxPawns.rank, usedTiles);
                if (availableTiles.Count == 0)
                {
                    Debug.LogError($"No available tiles for rank {maxPawns.rank}");
                    continue;
                }
                // Pick a random tile from available tiles
                int randomIndex = UnityEngine.Random.Range(0, availableTiles.Count);
                Tile selectedTile = availableTiles[randomIndex];
                // Find a pawn of this rank in purgatory
                List<PawnCommitment> vals = commitments.Values.ToList();
                foreach (PawnCommitment commitment in vals
                             .Where(commitment => commitment.starting_pos.ToVector2Int() == Globals.Purgatory)
                             .Where(commitment => Globals.FakeHashToPawnDef(commitment.pawn_def_hash).rank == (Rank)maxPawns.rank))
                {
                    PawnCommitment updatedCommitment = new()
                    {
                        pawn_def_hash = commitment.pawn_def_hash,
                        pawn_id = commitment.pawn_id,
                        starting_pos = new Pos(selectedTile.pos),
                    };
                    commitments[commitment.pawn_id] = updatedCommitment;
                    usedTiles.Add(selectedTile);
                    break;
                }
            }
        }
        SetupCommitReq req = new SetupCommitReq
        {
            lobby_id = lobby.index,
            setup_commitments = commitments.Values.ToArray(),
        };
        return req;
    }

    int CommitSetup(string address, SetupCommitReq req)
    {
        Lobby updatedLobby = fakeLobby;
        // Check if we're in the right phase (Setup phase)
        if (updatedLobby.phase != (uint)Phase.Setup)
        {
            Debug.LogError("Wrong phase for commit_setup");
            return (int)ErrorCode.WrongPhase;
        }
        // Get the appropriate user state based on the address
        UserState userState;
        UserState otherUserState;
        if (address == updatedLobby.host_address)
        {
            userState = updatedLobby.host_state;
            otherUserState = updatedLobby.guest_state;
        }
        else if (address == updatedLobby.guest_address)
        {
            userState = updatedLobby.guest_state;
            otherUserState = updatedLobby.host_state;
        }
        else
        {
            Debug.LogError($"Invalid user address: {address}");
            return (int)ErrorCode.InvalidArgs;
        }
        // Validate commitment count
        if (userState.setup_commitments.Length != req.setup_commitments.Length)
        {
            Debug.LogError("Invalid setup commitment count");
            return (int)ErrorCode.InvalidArgs;
        }
        // Update the user's setup commitments
        userState.setup_commitments = req.setup_commitments;
        userState.committed = true;
        if (updatedLobby.host_address == address)
        {
            updatedLobby.host_state = userState;
            updatedLobby.guest_state = otherUserState;
        }
        else
        {
            updatedLobby.guest_state = userState;
            updatedLobby.host_state = otherUserState;
        }
        // If both players have committed, advance to movement phase
        if (otherUserState.committed)
        {
            // Create a map of all commitments keyed by pawn_id
            Dictionary<string, PawnCommitment> commitmentMap = new Dictionary<string, PawnCommitment>();
            foreach (PawnCommitment commit in updatedLobby.host_state.setup_commitments)
            {
                commitmentMap[commit.pawn_id] = commit;
            }
            foreach (PawnCommitment commit in updatedLobby.guest_state.setup_commitments)
            {
                commitmentMap[commit.pawn_id] = commit;
            }
            // Update the pawns with the committed positions
            for (int i = 0; i < updatedLobby.pawns.Length; i++)
            {
                Contract.Pawn pawn = updatedLobby.pawns[i];
                if (commitmentMap.TryGetValue(pawn.pawn_id, out PawnCommitment commit))
                {
                    pawn.pos = commit.starting_pos;
                    pawn.pawn_def_hash = commit.pawn_def_hash;
                    pawn.is_alive = true;
                    updatedLobby.pawns[i] = pawn; // Update the pawn in the array
                }
            }
            // Initialize the first turn
            Turn firstTurn = new Turn
            {
                guest_events = Array.Empty<Contract.ResolveEvent>(),
                guest_events_hash = string.Empty,
                guest_turn = new TurnMove
                {
                    initialized = false,
                    pawn_id = string.Empty,
                    pos = new Pos { x = -666, y = -666 },
                    turn = 0,
                    user_address = updatedLobby.guest_address,
                },
                host_events = Array.Empty<Contract.ResolveEvent>(),
                host_events_hash = string.Empty,
                host_turn = new TurnMove
                {
                    initialized = false,
                    pawn_id = string.Empty,
                    pos = new Pos { x = -666, y = -666 },
                    turn = 0,
                    user_address = updatedLobby.host_address,
                },
                turn = 0,
            };
            // Add the first turn and change phase to Movement
            List<Turn> turns = new List<Turn>();
            turns.Add(firstTurn);
            updatedLobby.turns = turns.ToArray();
            updatedLobby.phase = (uint)Phase.Movement;
        }
        fakeLobby = updatedLobby;
        GameManager.instance.testBoardManager.FakeOnNetworkStateUpdated();
        return 0; // Success
    }

    void SubmitMove(MoveSubmitReq req)
    {
        
    }

    void ResolveMove(MoveResolveReq req)
    {
        
    }
}
