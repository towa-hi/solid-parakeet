using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;
using Random = UnityEngine.Random;
using Stellar;
using System.Collections.Immutable;
using UnityEngine.Assertions;

public static class FakeServer
{
    public static bool fakeIsOnline = false;
    public static LobbyParameters? fakeParameters = null;
    public static LobbyInfo? fakeLobbyInfo = null;
    public static GameState? fakeGameState = null;
    public static AccountAddress fakeHostAddress;
    public static AccountAddress fakeGuestAddress;
    public static User fakeHost;
    public static User fakeGuest;
    public static LobbyId fakeLobbyId = new LobbyId(42069);

    public static void Reset()
    {
        Debug.Log("FakeServer.Reset");
        fakeIsOnline = false;
        fakeParameters = null;
        fakeLobbyInfo = null;
        fakeGameState = null;
        fakeHost = default;
        fakeGuest = default;
        fakeHostAddress = default;
        fakeGuestAddress = default;
    }

    public static GameNetworkState GetFakeState()
    {
        Debug.Log("GetFakeState");
        Debug.Assert(fakeIsOnline);
        Debug.Assert(fakeParameters.HasValue);
        Debug.Assert(fakeLobbyInfo.HasValue);
        Debug.Assert(fakeGameState.HasValue);
        var gameNetworkState = new GameNetworkState(GetFakeNetworkState());
        gameNetworkState.lobbyInfo = fakeLobbyInfo.Value;
        gameNetworkState.lobbyParameters = fakeParameters.Value;
        gameNetworkState.gameState = fakeGameState.Value;
        return gameNetworkState;
    }
    public static NetworkState GetFakeNetworkState()
    {
        Debug.Log("GetFakeNetworkState");
        var networkState = new NetworkState(fakeHostAddress, false);
        networkState.user = fakeHost;
        networkState.lobbyInfo = fakeLobbyInfo;
        networkState.lobbyParameters = fakeParameters;
        networkState.gameState = fakeGameState;
        return networkState;
    }
    public static void MakeLobbyAsHost(LobbyParameters parameters)
    {
        Debug.Log("MakeLobbyAsHost");
        string hostSneed =ResourceRoot.DefaultSettings.defaultHostSneed;
        string guestSneed =ResourceRoot.DefaultSettings.defaultGuestSneed;
        fakeHostAddress = MuxedAccount.FromSecretSeed(hostSneed).AccountId;
        fakeGuestAddress = MuxedAccount.FromSecretSeed(guestSneed).AccountId;
        parameters.security_mode = false;
        Debug.Assert(parameters.security_mode == false);
        Debug.Assert(parameters.host_team == Team.RED);
        fakeIsOnline = true;
        fakeHost = new User {
            current_lobby = fakeLobbyId,
            games_completed = 0,
        };
        fakeParameters = parameters;
        fakeLobbyInfo = new LobbyInfo
        {
            guest_address = null,
            host_address = fakeHostAddress,
            index = fakeLobbyId,
        };
    }
    public static void JoinLobbyAsGuest(LobbyId lobbyId)
    {
        Debug.Log("JoinLobbyAsGuest");
        Debug.Assert(fakeIsOnline);
        fakeGuest = new User {
            current_lobby = fakeLobbyId,
            games_completed = 0,
        };
        LobbyInfo lobbyInfo = fakeLobbyInfo.Value;
        LobbyParameters parameters = fakeParameters.Value;

        Debug.Assert(lobbyInfo.phase == Phase.Lobby);

        lobbyInfo.guest_address = fakeGuestAddress;
        List<PawnState> pawns = new List<PawnState>();
        foreach (TileState tile in parameters.board.tiles)
        {
            if (tile.setup == Team.BLUE)
            {
                pawns.Add(new PawnState {
                    alive = true,
                    moved = false,
                    moved_scout = false,
                    pawn_id = new PawnId(tile.pos, Team.BLUE),
                    pos = tile.pos,
                    rank = null,
                    zz_revealed = false });
            }
            if (tile.setup == Team.RED)
            {
                pawns.Add(new PawnState {
                    alive = true,
                    moved = false,
                    moved_scout = false,
                    pawn_id = new PawnId(tile.pos, Team.RED),
                    pos = tile.pos,
                    rank = null,
                    zz_revealed = false });
            }
        }
        GameState gameState = new GameState {
            moves = new UserMove[] {
                new UserMove {
                    move_hashes = new byte[][] { },
                    move_proofs = new HiddenMove[] { },
                    needed_rank_proofs = new PawnId[] { },
                },
                new UserMove {
                    move_hashes = new byte[][] { },
                    move_proofs = new HiddenMove[] { },
                    needed_rank_proofs = new PawnId[] { },
                },
            },
            pawns = pawns.ToArray(),
            rank_roots = new byte[][] { },// not used in fake server
            turn = 0,
            liveUntilLedgerSeq = 0,
        };
        lobbyInfo.phase = Phase.SetupCommit;
        lobbyInfo.subphase = Subphase.Both;
        lobbyInfo.last_edited_ledger_seq = 0;
        fakeGameState = gameState;
        fakeLobbyInfo = lobbyInfo;
    }

    public static void CommitSetup(CommitSetupReq req, bool isHost)
    {
        Debug.Log("Fake CommitSetup");
        Debug.Assert(fakeIsOnline);
        int userIndex = isHost ? 0 : 1;
        LobbyInfo lobbyInfo = fakeLobbyInfo.Value;
        LobbyParameters parameters = fakeParameters.Value;
        GameState gameState = fakeGameState.Value;

        Debug.Assert(lobbyInfo.phase == Phase.SetupCommit);
        Dictionary<PawnId, Rank> hiddenRanks = new Dictionary<PawnId, Rank>();
        foreach (HiddenRank hiddenRank in req.zz_hidden_ranks)
        {
            hiddenRanks[hiddenRank.pawn_id] = hiddenRank.rank;
        }
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            PawnState pawn = gameState.pawns[i];
            if (hiddenRanks.TryGetValue(pawn.pawn_id, out Rank providedRank))
            {
                pawn.rank = providedRank;
                gameState.pawns[i] = pawn;
            }
        }
        Subphase nextSubphase = NextSubphase(lobbyInfo.subphase, isHost);
        if (nextSubphase == Subphase.None)
        {
            lobbyInfo.phase = Phase.MoveCommit;
            lobbyInfo.subphase = Subphase.Both;
            Debug.Log("Fake CommitSetup transitioned to movecommit");
        }
        else
        {
            lobbyInfo.subphase = nextSubphase;
        }
        fakeLobbyInfo = lobbyInfo;
        fakeGameState = gameState;
    }

    public static void CommitMoveAndProveMove(CommitMoveReq commitMoveReq, ProveMoveReq proveMoveReq, bool isHost)
    {
        Debug.Log("CommitMoveAndProveMove");
        Debug.Assert(fakeIsOnline);
        int userIndex = isHost ? 0 : 1;
        Debug.Assert(commitMoveReq.lobby_id == proveMoveReq.lobby_id);
        Debug.Assert(commitMoveReq.move_hashes.Length == proveMoveReq.move_proofs.Length);
        LobbyInfo lobbyInfo = fakeLobbyInfo.Value;
        LobbyParameters parameters = fakeParameters.Value;
        GameState gameState = fakeGameState.Value;

        // Minimal preconditions; avoid over-asserting
        if (!lobbyInfo.IsMySubphase(isHost ? fakeHostAddress : fakeGuestAddress) || lobbyInfo.phase != Phase.MoveCommit)
        {
            Debug.LogWarning($"[FakeServer] Unexpected state for commit: phase={lobbyInfo.phase} sub={lobbyInfo.subphase}");
        }
        Debug.Log($"[FakeServer] Pre-commit: turn={gameState.turn} phase={lobbyInfo.phase} subphase={lobbyInfo.subphase} isHost={isHost} userIndex={userIndex}");

        gameState.moves[userIndex].move_hashes = commitMoveReq.move_hashes;
        gameState.moves[userIndex].move_proofs = proveMoveReq.move_proofs;
        Debug.Log($"[FakeServer] Applied moves: userIndex={userIndex} hashes={commitMoveReq.move_hashes.Length} proofs={proveMoveReq.move_proofs.Length}");

        Subphase nextSubphase = NextSubphase(lobbyInfo.subphase, isHost);
        Debug.Log($"[FakeServer] NextSubphase={nextSubphase}");
        if (nextSubphase == Subphase.None)
        {
            try
            {
                // Use AiPlayer mechanics to resolve simultaneous moves reliably
                var simBoard = AiPlayer.MakeSimGameBoard(parameters, gameState);
                var baseState = simBoard.root_state;
                var moveSetBuilder = System.Collections.Immutable.ImmutableHashSet.CreateBuilder<AiPlayer.SimMove>();
                foreach (HiddenMove moveProof in gameState.moves[0].move_proofs)
                {
                    moveSetBuilder.Add(new AiPlayer.SimMove { last_pos = moveProof.start_pos, next_pos = moveProof.target_pos });
                }
                foreach (HiddenMove moveProof in gameState.moves[1].move_proofs)
                {
                    moveSetBuilder.Add(new AiPlayer.SimMove { last_pos = moveProof.start_pos, next_pos = moveProof.target_pos });
                }
                var moveSet = moveSetBuilder.ToImmutable();
                // Ensure no duplicate movers (same pawn moving twice). Duplicate targets allowed.
                // Compact: compare count vs distinct(last_pos)
                int distinctStarts = moveSet.Select(m => m.last_pos).Distinct().Count();
                if (distinctStarts != moveSet.Count)
                {
                    Debug.LogWarning("[FakeServer] Duplicate movers detected in move set");
                }
                var derived = AiPlayer.GetDerivedStateFromMove(simBoard, baseState, moveSet);
                // Sanity: turn advanced
                if (derived.turn != baseState.turn + 1)
                {
                    Debug.LogWarning($"[FakeServer] Derived turn unexpected: base={baseState.turn} derived={derived.turn}");
                }

                // Preserve original pawn index ordering when writing back
                PawnId[] originalOrder = gameState.pawns.Select(p => p.pawn_id).ToArray();
                Dictionary<PawnId, AiPlayer.SimPawn> idToSim = new Dictionary<PawnId, AiPlayer.SimPawn>();
                foreach (var kv in derived.pawns)
                {
                    idToSim[kv.Value.id] = kv.Value;
                }
                foreach (var dp in derived.dead_pawns)
                {
                    idToSim[dp.id] = dp;
                }
                PawnState[] rebuilt = new PawnState[originalOrder.Length];
                for (int i = 0; i < originalOrder.Length; i++)
                {
                    PawnId id = originalOrder[i];
                    PawnState prev = gameState.pawns[i];
                    if (idToSim.TryGetValue(id, out var simPawn))
                    {
                        prev.pos = simPawn.pos;
                        prev.alive = simPawn.alive;
                        prev.moved = simPawn.has_moved;
                        prev.zz_revealed = simPawn.is_revealed;
                    }
                    rebuilt[i] = prev;
                }
                gameState.pawns = rebuilt;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FakeServer] Resolve exception: {ex}");
                throw;
            }

            lobbyInfo.phase = Phase.MoveCommit;
            lobbyInfo.subphase = Subphase.Both;
            Debug.Log($"[FakeServer] Resolve complete: advancing turn {gameState.turn} -> {gameState.turn + 1}; pawns {gameState.pawns.Length}");
            gameState.turn++;
            // save changes
            fakeGameState = gameState;
            fakeLobbyInfo = lobbyInfo;
            return;
        }
        else
        {
            // Persist intermediate subphase and any move changes before returning
            lobbyInfo.subphase = nextSubphase;
            Debug.Log($"[FakeServer] Persist intermediate: subphase={lobbyInfo.subphase} turn={gameState.turn}");
            fakeGameState = gameState;
            fakeLobbyInfo = lobbyInfo;
            return;
        }
    }
    static Subphase NextSubphase(Subphase subphase, bool isHost)
    {
        return subphase switch
        {
            Subphase.Host => isHost ? Subphase.None : Subphase.Guest,
            Subphase.Guest => isHost ? Subphase.Host : Subphase.None,
            Subphase.Both => isHost? Subphase.Guest : Subphase.Host,
            Subphase.None => throw new Exception("Invalid subphase"),
        };
    }

    static (PawnState?, PawnState?, PawnState?) BattlePawns(PawnState a, PawnState b)
    {
        if (a.rank == Rank.TRAP && b.rank == Rank.SEER)
        {
            return (b, a, null);
        }
        if (a.rank == Rank.SEER && b.rank == Rank.TRAP)
        {
            return (a, b, null);
        }
        if (a.rank == Rank.WARLORD && b.rank == Rank.ASSASSIN)
        {
            return (b, a, null);
        }
        if (a.rank == Rank.ASSASSIN && b.rank == Rank.WARLORD)
        {
            return (a, b, null);
        }
        if (a.rank < b.rank)
        {
            return (b, a, null);
        }
        if (a.rank > b.rank)
        {
            return (a, b, null);
        }
        return (null, a, b);
    }

    public static List<HiddenMove> TempFakeHiddenMoves(Team team)
    {
        uint lesser_move_threshold = 3; // Increase to make have it randomly make worse moves.
        Debug.Assert(fakeIsOnline);
        LobbyParameters parameters = fakeParameters.Value;
        GameState gameState = fakeGameState.Value;
        var board = AiPlayer.MakeSimGameBoard(parameters, gameState);
        board.ally_team = team;
        AiPlayer.MutGuessOpponentRanks(board, board.root_state);
        var top_moves = AiPlayer.NodeScoreStrategy(board, board.root_state);
        Debug.Log($"top_moves {top_moves.Count}");
        var max_moves = AiPlayer.MaxMovesThisTurn(board, board.root_state.turn);
        ImmutableHashSet<AiPlayer.SimMove> moves = null;
        // Pick random top move and also assemble blitz move if needed.
        if (max_moves > 1)
        {
            top_moves = AiPlayer.CombineMoves(top_moves, max_moves, lesser_move_threshold);
        }
        moves = top_moves[(int)Random.Range(0, Mathf.Min(top_moves.Count - 1, lesser_move_threshold))];
        // Convert to HiddenMoves.
        List<HiddenMove> result_moves = new();
        foreach (var move in moves)
        {
            result_moves.Add(new HiddenMove()
            {
                pawn_id = board.root_state.pawns[move.last_pos].id,
                start_pos = move.last_pos,
                target_pos = move.next_pos,
            });
        }
        return result_moves;
    }


    // public Contract.LobbyParameters fakeParameters;
    // public Contract.Lobby fakeLobby;
    // public Contract.User fakeHost;
    // public Contract.User fakeGuest;
    // public uint fakeLobbyId = 1;
    //
    // public static FakeServer ins;
    // public BoardDef boardDef;
    //
    // void Awake()
    // {
    //     ins = this;
    //     fakeGuest = new User
    //     {
    //         current_lobby = fakeLobbyId,
    //         games_completed = 0,
    //     };
    //     fakeHost = new User
    //     {
    //         current_lobby = fakeLobbyId,
    //         games_completed = 0,
    //     };
    // }

    //
    // public void StartFakeLobby()
    // {
    //     // List<Contract.Pawn> pawnsList = new List<Contract.Pawn>();
    //     // List<PawnCommitment> hostCommitments = new List<PawnCommitment>();
    //     // List<PawnCommitment> guestCommitments = new List<PawnCommitment>();
    //     //
    //     // foreach (MaxPawns maxPawn in fakeParameters.max_pawns)
    //     // {
    //     //     PawnDef def = Globals.RankToPawnDef((Rank)maxPawn.rank);
    //     //     for (int i = 0; i < maxPawn.max; i++)
    //     //     {
    //     //         Contract.Pawn pawn = new Contract.Pawn
    //     //         {
    //     //             is_alive = false,
    //     //             is_moved = false,
    //     //             is_revealed = false,
    //     //             pawn_def_hash = Globals.PawnDefToFakeHash(def),
    //     //             pawn_id = Guid.NewGuid().ToString(),
    //     //             pos = new Pos(Globals.Purgatory),
    //     //             team = 1,
    //     //         };
    //     //         PawnCommitment commitment = new PawnCommitment
    //     //         {
    //     //             pawn_def_hash = pawn.pawn_def_hash,
    //     //             pawn_id = pawn.pawn_id,
    //     //             starting_pos = pawn.pos,
    //     //         };
    //     //         pawnsList.Add(pawn);
    //     //         hostCommitments.Add(commitment);
    //     //     }
    //     // }
    //     // foreach (MaxPawns maxPawn in fakeParameters.max_pawns)
    //     // {
    //     //     PawnDef def = Globals.RankToPawnDef((Rank)maxPawn.rank);
    //     //     for (int i = 0; i < maxPawn.max; i++)
    //     //     {
    //     //         Contract.Pawn pawn = new Contract.Pawn
    //     //         {
    //     //             is_alive = false,
    //     //             is_moved = false,
    //     //             is_revealed = false,
    //     //             pawn_def_hash = Globals.PawnDefToFakeHash(def),
    //     //             pawn_id = Guid.NewGuid().ToString(),
    //     //             pos = new Pos(Globals.Purgatory),
    //     //             team = 2,
    //     //         };
    //     //         PawnCommitment commitment = new PawnCommitment
    //     //         {
    //     //             pawn_def_hash = pawn.pawn_def_hash,
    //     //             pawn_id = pawn.pawn_id,
    //     //             starting_pos = pawn.pos,
    //     //         };
    //     //         pawnsList.Add(pawn);
    //     //         guestCommitments.Add(commitment);
    //     //     }
    //     // }
    //     // fakeLobby = new Lobby
    //     // {
    //     //     game_end_state = 3,
    //     //     guest_address = fakeGuest.index,
    //     //     guest_state = new UserState
    //     //     {
    //     //         committed = false,
    //     //         lobby_state = 2,
    //     //         setup_commitments = guestCommitments.ToArray(),
    //     //         team = 2,
    //     //         user_address = fakeGuest.index,
    //     //     },
    //     //     host_address = fakeHost.index,
    //     //     host_state = new UserState
    //     //     {
    //     //         committed = false,
    //     //         lobby_state = 2,
    //     //         setup_commitments = hostCommitments.ToArray(),
    //     //         team = 1,
    //     //         user_address = fakeHost.index,
    //     //     },
    //     //     index = fakeLobbyId,
    //     //     parameters = fakeParameters,
    //     //     pawns = pawnsList.ToArray(),
    //     //     phase = 1,
    //     //     turns = new Turn[] {},
    //     // };
    //     // fakeLobby is updated to the result of these functions
    //     // host -> make_lobby
    //     // guest -> join_lobby
    // }
    //
    // //
    // // public int CommitSetupRequest(Dictionary<string, PawnCommitment> commitments)
    // // {
    // //     // Convert dictionary to array for the host's setup commitment request
    // //     PawnCommitment[] setupCommitments = new PawnCommitment[commitments.Count];
    // //     for (int i = 0; i < commitments.Count; i++)
    // //     {
    // //         KeyValuePair<string, PawnCommitment> kvp = commitments.ElementAt(i);
    // //         PawnCommitment commitment = kvp.Value;
    // //         setupCommitments[i] = commitment;
    // //     }
    // //
    // //     // Create the host's setup commit request
    // //     SetupCommitReq hostReq = new()
    // //     {
    // //         lobby_id = fakeLobby.index,
    // //         setup_commitments = setupCommitments,
    // //     };
    // //     // Generate and process the guest's request
    // //     SetupCommitReq guestReq = GuestSetupCommitReq(fakeGuest, fakeLobby);
    // //     int guestResult = CommitSetup(fakeGuest.index, guestReq);
    // //     if (guestResult != 0)
    // //     {
    // //         Debug.LogError($"Guest commit failed with error code {guestResult}");
    // //         return guestResult;
    // //     }
    // //     // Process the host's request
    // //     int hostResult = CommitSetup(fakeHost.index, hostReq);
    // //     if (hostResult != 0)
    // //     {
    // //         Debug.LogError($"Host commit failed with error code {hostResult}");
    // //         return hostResult;
    // //     }
    // //     return 0; // Success
    // // }
    //
    // public int QueueMove(QueuedMove queuedMove)
    // {
    //     // Turn currentTurn = fakeLobby.turns.Last();
    //     //
    //     // MoveSubmitReq guestReq = GuestMoveSubmitReq(fakeGuest, fakeLobby);
    //     // int guestResult = SubmitMove(fakeGuest.index, guestReq);
    //     // if (guestResult != 0)
    //     // {
    //     //     Debug.LogError($"Guest commit failed with error code {guestResult}");
    //     //     return guestResult;
    //     // }
    //     // MoveSubmitReq hostReq = new()
    //     // {
    //     //     lobby = fakeLobby.index,
    //     //     move_pos = new Pos(queuedMove.pos),
    //     //     pawn_id = queuedMove.pawnId.ToString(),
    //     //     turn = currentTurn.host_turn.turn,
    //     //     user_address = fakeHost.index,
    //     // };
    //     // int hostResult = SubmitMove(fakeHost.index, hostReq);
    //     // if (hostResult != 0)
    //     // {
    //     //     Debug.LogError($"Host commit failed with error code {hostResult}");
    //     // }
    //     return 0;
    // }
    //
    // public int SubmitMoveHash()
    // {
    //     // MoveResolveReq guestReq = Globals.ResolveTurn(fakeLobby, fakeGuest.index);
    //     // guestReq.user_address = fakeGuest.index;
    //     // int guestResult = ResolveMove(fakeGuest.index, guestReq);
    //     // if (guestResult != 0)
    //     // {
    //     //     Debug.LogError($"Guest commit failed with error code {guestResult}");
    //     //     return guestResult;
    //     // }
    //     // MoveResolveReq hostReq = Globals.ResolveTurn(fakeLobby, fakeHost.index);
    //     // hostReq.user_address = fakeHost.index;
    //     // int hostResult = ResolveMove(fakeHost.index, hostReq);
    //     // if (hostResult != 0)
    //     // {
    //     //     Debug.LogError($"Host commit failed with error code {hostResult}");
    //     //     return hostResult;
    //     // }
    //     return 0;
    // }
    //
    // CommitSetupReq GuestSetupCommitReq(User guest, Lobby lobby)
    // {
    //     // // Get the guest's user state and commitments
    //     // UserState guestState = lobby.guest_state;
    //     // Dictionary<string, PawnCommitment> commitments = guestState.setup_commitments.ToDictionary(commitment => commitment.pawn_id);
    //     // // We need to generate valid positions for each pawn commitment
    //     // // Similar to SetupTestPhase.OnAutoSetup
    //     // HashSet<Tile> usedTiles = new();
    //     // // sort maxPawns by def
    //     // Contract.MaxPawns[] sortedMaxPawns = lobby.parameters.max_pawns;
    //     // Array.Sort(sortedMaxPawns, (maxPawns1, maxPawns2) => Rules.GetSetupZone((Rank)maxPawns1.rank) < Rules.GetSetupZone((Rank)maxPawns2.rank) ? 1 : -1);
    //     // foreach (MaxPawns maxPawns in sortedMaxPawns)
    //     // {
    //     //     for (int i = 0; i < maxPawns.max; i++)
    //     //     {
    //     //         // Get available tiles for this rank
    //     //         List<Tile> availableTiles = boardDef.GetEmptySetupTiles(lobby.GetTeam(guest.index), (Rank)maxPawns.rank, usedTiles);
    //     //         if (availableTiles.Count == 0)
    //     //         {
    //     //             Debug.LogError($"No available tiles for rank {maxPawns.rank}");
    //     //             continue;
    //     //         }
    //     //         // Pick a random tile from available tiles
    //     //         int randomIndex = UnityEngine.Random.Range(0, availableTiles.Count);
    //     //         Tile selectedTile = availableTiles[randomIndex];
    //     //         // Find a pawn of this rank in purgatory
    //     //         List<PawnCommitment> vals = commitments.Values.ToList();
    //     //         foreach (PawnCommitment commitment in vals
    //     //                      .Where(commitment => commitment.starting_pos.ToVector2Int() == Globals.Purgatory)
    //     //                      .Where(commitment => Globals.FakeHashToPawnDef(commitment.pawn_def_hash).rank == (Rank)maxPawns.rank))
    //     //         {
    //     //             PawnCommitment updatedCommitment = new()
    //     //             {
    //     //                 pawn_def_hash = commitment.pawn_def_hash,
    //     //                 pawn_id = commitment.pawn_id,
    //     //                 starting_pos = new Pos(selectedTile.pos),
    //     //             };
    //     //             commitments[commitment.pawn_id] = updatedCommitment;
    //     //             usedTiles.Add(selectedTile);
    //     //             break;
    //     //         }
    //     //     }
    //     // }
    //     // SetupCommitReq req = new SetupCommitReq
    //     // {
    //     //     lobby_id = lobby.index,
    //     //     setup_commitments = commitments.Values.ToArray(),
    //     // };
    //     // return req;
    //     return new CommitSetupReq()
    //     {
    //
    //     };
    // }
    //
    // // MoveSubmitReq GuestMoveSubmitReq(User guest, Lobby lobby)
    // // {
    // //     // pick a move
    // //     Dictionary<Contract.Pawn, HashSet<Vector2Int>> validMoves = new();
    // //     foreach (var pawn in lobby.pawns)
    // //     {
    // //         if (pawn.is_alive && pawn.team == lobby.guest_state.team)
    // //         {
    // //             HashSet<Vector2Int> movablePositions = GetMovablePositions(pawn, lobby, boardDef);
    // //             if (movablePositions.Count > 0)
    // //             {
    // //                 validMoves.Add(pawn, movablePositions);
    // //             }
    // //         }
    // //     }
    // //
    // //     if (validMoves.Count == 0)
    // //     {
    // //         throw new Exception("No valid moves for guest");
    // //     }
    // //     Contract.Pawn randomPawn = validMoves.Keys.ElementAt(Random.Range(0, validMoves.Count));
    // //     Vector2Int randomMove = validMoves[randomPawn].ElementAt(Random.Range(0, validMoves[randomPawn].Count));
    // //     MoveSubmitReq req = new()
    // //     {
    // //         lobby = lobby.index,
    // //         move_pos = new Pos(randomMove),
    // //         pawn_id = randomPawn.pawn_id,
    // //         turn = lobby.GetLatestTurn().turn,
    // //         user_address = "",
    // //     };
    // //     return req;
    // // }
    //
    // // static HashSet<Vector2Int> GetMovablePositions(Contract.Pawn pawn, Lobby lobby, BoardDef boardDef)
    // // {
    // //     List<Tile> movableTiles = new();
    // //     HashSet<Vector2Int> movablePositions = new();
    // //     PawnDef def = Globals.FakeHashToPawnDef(pawn.pawn_def_hash);
    // //     if (!pawn.is_alive || def.movementRange == 0)
    // //     {
    // //         return movablePositions;
    // //     }
    // //     Vector2Int pawnPos = pawn.pos.ToVector2Int();
    // //     Vector2Int[] initialDirections = Shared.GetDirections(pawnPos, boardDef.isHex);
    // //     for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
    // //     {
    // //         Vector2Int currentPos = pawnPos;
    // //         int walkedTiles = 0;
    // //         while (walkedTiles < def.movementRange)
    // //         {
    // //             Vector2Int[] currentDirections = Shared.GetDirections(currentPos, boardDef.isHex);
    // //             currentPos += currentDirections[dirIndex];
    // //             Tile tile = boardDef.GetTileByPos(currentPos);
    // //             if (tile == null || !tile.isPassable) break;
    // //             Contract.Pawn? maybePawn = lobby.GetPawnByPosition(currentPos);
    // //             if (maybePawn.HasValue)
    // //             {
    // //                 if (maybePawn.Value.team == pawn.team)
    // //                 {
    // //                     break;
    // //                 }
    // //                 movablePositions.Add(currentPos);
    // //             }
    // //             movablePositions.Add(currentPos);
    // //             walkedTiles++;
    // //         }
    // //     }
    // //     return movablePositions;
    // // }
    // //
    // // int CommitSetup(string address, SetupCommitReq req)
    // // {
    // //     Lobby updatedLobby = fakeLobby;
    // //     // Check if we're in the right phase (Setup phase)
    // //     if (updatedLobby.phase != (uint)Phase.Setup)
    // //     {
    // //         Debug.LogError("Wrong phase for commit_setup");
    // //         return (int)ErrorCode.WrongPhase;
    // //     }
    // //     // Get the appropriate user state based on the address
    // //     UserState userState;
    // //     UserState otherUserState;
    // //     if (address == updatedLobby.host_address)
    // //     {
    // //         userState = updatedLobby.host_state;
    // //         otherUserState = updatedLobby.guest_state;
    // //     }
    // //     else if (address == updatedLobby.guest_address)
    // //     {
    // //         userState = updatedLobby.guest_state;
    // //         otherUserState = updatedLobby.host_state;
    // //     }
    // //     else
    // //     {
    // //         Debug.LogError($"Invalid user address: {address}");
    // //         return (int)ErrorCode.InvalidArgs;
    // //     }
    // //     // Validate commitment count
    // //     if (userState.setup_commitments.Length != req.setup_commitments.Length)
    // //     {
    // //         Debug.LogError("Invalid setup commitment count");
    // //         return (int)ErrorCode.InvalidArgs;
    // //     }
    // //     // Update the user's setup commitments
    // //     userState.setup_commitments = req.setup_commitments;
    // //     userState.committed = true;
    // //     if (updatedLobby.host_address == address)
    // //     {
    // //         updatedLobby.host_state = userState;
    // //         updatedLobby.guest_state = otherUserState;
    // //     }
    // //     else
    // //     {
    // //         updatedLobby.guest_state = userState;
    // //         updatedLobby.host_state = otherUserState;
    // //     }
    // //     // If both players have committed, advance to movement phase
    // //     if (otherUserState.committed)
    // //     {
    // //         // Create a map of all commitments keyed by pawn_id
    // //         Dictionary<string, PawnCommitment> commitmentMap = new Dictionary<string, PawnCommitment>();
    // //         foreach (PawnCommitment commit in updatedLobby.host_state.setup_commitments)
    // //         {
    // //             commitmentMap[commit.pawn_id] = commit;
    // //         }
    // //         foreach (PawnCommitment commit in updatedLobby.guest_state.setup_commitments)
    // //         {
    // //             commitmentMap[commit.pawn_id] = commit;
    // //         }
    // //         // Update the pawns with the committed positions
    // //         for (int i = 0; i < updatedLobby.pawns.Length; i++)
    // //         {
    // //             Contract.Pawn pawn = updatedLobby.pawns[i];
    // //             if (commitmentMap.TryGetValue(pawn.pawn_id, out PawnCommitment commit))
    // //             {
    // //                 pawn.pos = commit.starting_pos;
    // //                 pawn.pawn_def_hash = commit.pawn_def_hash;
    // //                 pawn.is_alive = true;
    // //                 updatedLobby.pawns[i] = pawn; // Update the pawn in the array
    // //             }
    // //         }
    // //         // Initialize the first turn
    // //         Turn firstTurn = new Turn
    // //         {
    // //             guest_events = Array.Empty<Contract.ResolveEvent>(),
    // //             guest_events_hash = string.Empty,
    // //             guest_turn = new TurnMove
    // //             {
    // //                 initialized = false,
    // //                 pawn_id = string.Empty,
    // //                 pos = new Pos { x = -666, y = -666 },
    // //                 turn = 0,
    // //                 user_address = updatedLobby.guest_address,
    // //             },
    // //             host_events = Array.Empty<Contract.ResolveEvent>(),
    // //             host_events_hash = string.Empty,
    // //             host_turn = new TurnMove
    // //             {
    // //                 initialized = false,
    // //                 pawn_id = string.Empty,
    // //                 pos = new Pos { x = -666, y = -666 },
    // //                 turn = 0,
    // //                 user_address = updatedLobby.host_address,
    // //             },
    // //             turn = 0,
    // //         };
    // //         // Add the first turn and change phase to Movement
    // //         List<Turn> turns = new List<Turn>();
    // //         turns.Add(firstTurn);
    // //         updatedLobby.turns = turns.ToArray();
    // //         updatedLobby.phase = (uint)Phase.Movement;
    // //     }
    // //     fakeLobby = updatedLobby;
    // //     if (address == "")
    // //     {
    // //         GameManager.instance.testBoardManager.FakeOnNetworkStateUpdated();
    // //     }
    // //     return 0; // Success
    // // }
    //
    // int SubmitMove(string address, MoveSubmitReq req)
    // {
    //     // Lobby updatedLobby = fakeLobby;
    //     // Turn turn = updatedLobby.GetLatestTurn();
    //     // if (req.user_address == turn.host_turn.user_address)
    //     // {
    //     //     if (turn.host_turn.initialized)
    //     //     {
    //     //         return (int)ErrorCode.TurnAlreadyInitialized;
    //     //     }
    //     //     turn.host_turn.initialized = true;
    //     //     turn.host_turn.pos = req.move_pos;
    //     //     turn.host_turn.pawn_id = req.pawn_id;
    //     //     updatedLobby.turns[^1] = turn;
    //     // } else if (req.user_address == turn.guest_turn.user_address)
    //     // {
    //     //     if (turn.guest_turn.initialized)
    //     //     {
    //     //         return (int)ErrorCode.TurnAlreadyInitialized;
    //     //     }
    //     //     turn.guest_turn.initialized = true;
    //     //     turn.guest_turn.pos = req.move_pos;
    //     //     turn.guest_turn.pawn_id = req.pawn_id;
    //     //     updatedLobby.turns[^1] = turn;
    //     // }
    //     // else
    //     // {
    //     //     return (int)ErrorCode.InvalidArgs;
    //     // }
    //     // fakeLobby = updatedLobby;
    //     // if (address == "")
    //     // {
    //     //     GameManager.instance.testBoardManager.FakeOnNetworkStateUpdated();
    //     // }
    //     return 0;
    // }
    //
    // int ResolveMove(string address, MoveResolveReq req)
    // {
    //     // Lobby updatedLobby = fakeLobby;
    //     // Turn turn = updatedLobby.GetLatestTurn();
    //     // if (req.user_address == turn.host_turn.user_address)
    //     // {
    //     //     if (turn.host_turn.initialized)
    //     //     {
    //     //         turn.host_events = req.events;
    //     //         turn.host_events_hash = req.events_hash;
    //     //     }
    //     //     else
    //     //     {
    //     //         return (int)ErrorCode.InvalidArgs;
    //     //     }
    //     // }
    //     // else if (req.user_address == turn.guest_turn.user_address)
    //     // {
    //     //     if (turn.guest_turn.initialized)
    //     //     {
    //     //         turn.guest_events = req.events;
    //     //         turn.guest_events_hash = req.events_hash;
    //     //     }
    //     //     else
    //     //     {
    //     //         return (int)ErrorCode.InvalidArgs;
    //     //     }
    //     // }
    //     // else
    //     // {
    //     //     return (int)ErrorCode.InvalidArgs;
    //     // }
    //     // updatedLobby.turns[^1] = turn;
    //     // if (!string.IsNullOrEmpty(turn.host_events_hash) && !string.IsNullOrEmpty(turn.guest_events_hash))
    //     // {
    //     //     if (turn.host_events_hash == turn.guest_events_hash)
    //     //     {
    //     //         Pos purgatory = new Pos(Globals.Purgatory);
    //     //         foreach (var resolveEvent in req.events)
    //     //         {
    //     //             switch (resolveEvent.event_type)
    //     //             {
    //     //                 case 0: // move
    //     //                     for (int i = 0; i < updatedLobby.pawns.Length; i++)
    //     //                     {
    //     //                         Contract.Pawn updatedPawn = updatedLobby.pawns[i];
    //     //                         if (updatedPawn.pawn_id == resolveEvent.pawn_id)
    //     //                         {
    //     //                             updatedPawn.pos = resolveEvent.target_pos;
    //     //                             updatedPawn.is_moved = true;
    //     //                             updatedLobby.pawns[i] = updatedPawn;
    //     //                             break;
    //     //                         }
    //     //                     }
    //     //                     break;
    //     //                 case 1: // conflict
    //     //                     for (int i = 0; i < updatedLobby.pawns.Length; i++)
    //     //                     {
    //     //                         Contract.Pawn updatedPawn = updatedLobby.pawns[i];
    //     //                         int processedCount = 0;
    //     //                         if (updatedPawn.pawn_id == resolveEvent.pawn_id || updatedPawn.pawn_id == resolveEvent.defender_pawn_id)
    //     //                         {
    //     //                             updatedPawn.pos = resolveEvent.target_pos;
    //     //                             updatedPawn.is_revealed = true;
    //     //                             updatedLobby.pawns[i] = updatedPawn;
    //     //                             processedCount++;
    //     //                         }
    //     //                         if (processedCount >= 2)
    //     //                         {
    //     //                             break;
    //     //                         }
    //     //                     }
    //     //                     break;
    //     //                 case 2: // swapConflict
    //     //                     for (int i = 0; i < updatedLobby.pawns.Length; i++)
    //     //                     {
    //     //                         Contract.Pawn updatedPawn = updatedLobby.pawns[i];
    //     //                         int processedCount = 0;
    //     //                         if (updatedPawn.pawn_id == resolveEvent.pawn_id || updatedPawn.pawn_id == resolveEvent.defender_pawn_id)
    //     //                         {
    //     //                             updatedPawn.pos = resolveEvent.target_pos;
    //     //                             updatedPawn.is_revealed = true;
    //     //                             updatedLobby.pawns[i] = updatedPawn;
    //     //                             processedCount++;
    //     //                         }
    //     //                         if (processedCount >= 2)
    //     //                         {
    //     //                             break;
    //     //                         }
    //     //                     }
    //     //                     break;
    //     //                 case 3: // death
    //     //                     for (int i = 0; i < updatedLobby.pawns.Length; i++)
    //     //                     {
    //     //                         Contract.Pawn updatedPawn = updatedLobby.pawns[i];
    //     //                         if (updatedPawn.pawn_id == resolveEvent.pawn_id)
    //     //                         {
    //     //                             updatedPawn.is_alive = false;
    //     //                             updatedPawn.pos = purgatory;
    //     //                             updatedLobby.pawns[i] = updatedPawn;
    //     //                             break;
    //     //                         }
    //     //                     }
    //     //                     break;
    //     //                 default:
    //     //                     break;
    //     //             }
    //     //         }
    //     //         bool redThroneAlive = false;
    //     //         bool blueThroneAlive = false;
    //     //         for (int i = 0; i < updatedLobby.pawns.Length; i++)
    //     //         {
    //     //             Contract.Pawn pawn = updatedLobby.pawns[i];
    //     //             if (pawn.pawn_def_hash == "Throne" && pawn.is_alive)
    //     //             {
    //     //                 if ((Team)pawn.team == Team.RED)
    //     //                 {
    //     //                     redThroneAlive = true;
    //     //                 }
    //     //                 if ((Team)pawn.team == Team.BLUE)
    //     //                 {
    //     //                     blueThroneAlive = true;
    //     //                 }
    //     //             }
    //     //         }
    //     //         if (!redThroneAlive)
    //     //         {
    //     //             updatedLobby.game_end_state = 2;
    //     //         }
    //     //
    //     //         if (!blueThroneAlive)
    //     //         {
    //     //             updatedLobby.game_end_state = 1;
    //     //         }
    //     //         if (!redThroneAlive && !blueThroneAlive)
    //     //         {
    //     //             updatedLobby.game_end_state = 0;
    //     //         }
    //     //         Turn nextTurn = new Turn
    //     //         {
    //     //             guest_events = Array.Empty<Contract.ResolveEvent>(),
    //     //             guest_events_hash = string.Empty,
    //     //             guest_turn = new TurnMove
    //     //             {
    //     //                 initialized = false,
    //     //                 pawn_id = string.Empty,
    //     //                 pos = new Pos { x = -666, y = -666 },
    //     //                 turn = updatedLobby.turns.Length,
    //     //                 user_address = updatedLobby.guest_address,
    //     //             },
    //     //             host_events = Array.Empty<Contract.ResolveEvent>(),
    //     //             host_events_hash = string.Empty,
    //     //             host_turn = new TurnMove
    //     //             {
    //     //                 initialized = false,
    //     //                 pawn_id = string.Empty,
    //     //                 pos = new Pos { x = -666, y = -666 },
    //     //                 turn = updatedLobby.turns.Length,
    //     //                 user_address = updatedLobby.host_address,
    //     //             },
    //     //             turn = updatedLobby.turns.Length,
    //     //         };
    //     //         List<Turn> turnsList = updatedLobby.turns.ToList();
    //     //         turnsList.Add(nextTurn);
    //     //         updatedLobby.turns = turnsList.ToArray();
    //     //     }
    //     //     else
    //     //     {
    //     //         return (int)ErrorCode.TurnHashConflict;
    //     //     }
    //     // }
    //     // fakeLobby = updatedLobby;
    //     // if (address == "")
    //     // {
    //     //     GameManager.instance.testBoardManager.FakeOnNetworkStateUpdated();
    //     // }
    //     return 0;
    // }
}
