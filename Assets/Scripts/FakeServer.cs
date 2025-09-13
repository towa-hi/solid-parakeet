using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;
using Random = UnityEngine.Random;
using Stellar;
using System.Collections.Immutable;
using UnityEngine.Assertions;
using System.Threading.Tasks;

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
        parameters.blitz_interval = 0;
        parameters.blitz_max_simultaneous_moves = 1;
        fakeParameters = parameters;
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
                Dictionary<PawnId, AiPlayer.SimPawn> idToSim = new Dictionary<PawnId, AiPlayer.SimPawn>(derived.dead_pawns);
                foreach (var kv in derived.pawns)
                {
                    idToSim[kv.Value.id] = kv.Value;
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
            _ => throw new ArgumentOutOfRangeException(nameof(subphase), subphase, null)
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

    public static async Task<List<HiddenMove>> TempFakeHiddenMoves(Team team)
    {
        uint lesser_move_threshold = 0; // Increase to make have it randomly make worse moves.
        Debug.Assert(fakeIsOnline);
        LobbyParameters parameters = fakeParameters.Value;
        GameState gameState = fakeGameState.Value;
        var board = AiPlayer.MakeSimGameBoard(parameters, gameState);
        board.ally_team = team;
        //AiPlayer.MutGuessOpponentRanks(board, board.root_state);
        var top_moves = await AiPlayer.NodeScoreStrategy(board, board.root_state);
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
}
