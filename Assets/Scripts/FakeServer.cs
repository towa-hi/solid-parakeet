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
        var networkState = new NetworkState();
        networkState.fromOnline = false;
        networkState.address = fakeHostAddress;
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
        // allow host_team to be RED or BLUE in single-player
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

    public static void Resign(bool isHost)
    {
        Debug.Log($"FakeServer.Resign isHost={isHost}");
        Debug.Assert(fakeIsOnline);
        Debug.Assert(fakeLobbyInfo.HasValue);
        LobbyInfo lobbyInfo = fakeLobbyInfo.Value;
        // If already finished, ignore
        if (lobbyInfo.phase == Phase.Finished || lobbyInfo.phase == Phase.Aborted)
        {
            Debug.Log("FakeServer.Resign ignored: game already finished");
            return;
        }
        lobbyInfo.phase = Phase.Finished;
        lobbyInfo.subphase = isHost ? Subphase.Guest : Subphase.Host;
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
                // Diagnostics: map pawn_id to current position
                var idToPos = new Dictionary<PawnId, Vector2Int>();
                foreach (var kv in baseState.pawns)
                {
                    idToPos[kv.Value.id] = kv.Key;
                }
                var moveSetBuilder = System.Collections.Immutable.ImmutableHashSet.CreateBuilder<AiPlayer.SimMove>();
                int missingStart = 0;
                int mismatchedId = 0;
                // host moves
                foreach (HiddenMove moveProof in gameState.moves[0].move_proofs)
                {
                    bool hasStart = baseState.pawns.TryGetValue(moveProof.start_pos, out var atStart);
                    if (!hasStart)
                    {
                        missingStart++;
                        bool foundById = idToPos.TryGetValue(moveProof.pawn_id, out var actualPos);
                        bool isDead = baseState.dead_pawns.ContainsKey(moveProof.pawn_id);
                        Debug.LogWarning($"[FakeServer] Pre-resolve: missing start pawn (host). start={moveProof.start_pos} target={moveProof.target_pos} pawnId={moveProof.pawn_id} foundById={foundById} actualPos={(foundById ? actualPos : new Vector2Int(-1, -1))} deadById={isDead}");
                    }
                    else if (atStart.id != moveProof.pawn_id)
                    {
                        mismatchedId++;
                        bool foundById = idToPos.TryGetValue(moveProof.pawn_id, out var actualPos);
                        bool isDead = baseState.dead_pawns.ContainsKey(moveProof.pawn_id);
                        Debug.LogWarning($"[FakeServer] Pre-resolve: pawn_id mismatch at start (host). start={moveProof.start_pos} target={moveProof.target_pos} expectedId={atStart.id} gotId={moveProof.pawn_id} foundById={foundById} actualPos={(foundById ? actualPos : new Vector2Int(-1, -1))} deadById={isDead}");
                    }
                    moveSetBuilder.Add(new AiPlayer.SimMove { last_pos = moveProof.start_pos, next_pos = moveProof.target_pos });
                }
                // guest moves
                foreach (HiddenMove moveProof in gameState.moves[1].move_proofs)
                {
                    bool hasStart = baseState.pawns.TryGetValue(moveProof.start_pos, out var atStart);
                    if (!hasStart)
                    {
                        missingStart++;
                        bool foundById = idToPos.TryGetValue(moveProof.pawn_id, out var actualPos);
                        bool isDead = baseState.dead_pawns.ContainsKey(moveProof.pawn_id);
                        Debug.LogWarning($"[FakeServer] Pre-resolve: missing start pawn (guest). start={moveProof.start_pos} target={moveProof.target_pos} pawnId={moveProof.pawn_id} foundById={foundById} actualPos={(foundById ? actualPos : new Vector2Int(-1, -1))} deadById={isDead}");
                    }
                    else if (atStart.id != moveProof.pawn_id)
                    {
                        mismatchedId++;
                        bool foundById = idToPos.TryGetValue(moveProof.pawn_id, out var actualPos);
                        bool isDead = baseState.dead_pawns.ContainsKey(moveProof.pawn_id);
                        Debug.LogWarning($"[FakeServer] Pre-resolve: pawn_id mismatch at start (guest). start={moveProof.start_pos} target={moveProof.target_pos} expectedId={atStart.id} gotId={moveProof.pawn_id} foundById={foundById} actualPos={(foundById ? actualPos : new Vector2Int(-1, -1))} deadById={isDead}");
                    }
                    moveSetBuilder.Add(new AiPlayer.SimMove { last_pos = moveProof.start_pos, next_pos = moveProof.target_pos });
                }
                if (missingStart > 0 || mismatchedId > 0)
                {
                    Debug.LogWarning($"[FakeServer] Pre-resolve summary: missingStart={missingStart} mismatchedId={mismatchedId} turn={baseState.turn} pawns={baseState.pawns.Count}");
                }
                var moveSet = moveSetBuilder.ToImmutable();
                // Detect swap pairs for diagnostics
                int swapPairs = 0;
                var moveSetLookup = new HashSet<(Vector2Int, Vector2Int)>();
                foreach (var m in moveSet) moveSetLookup.Add((m.last_pos, m.next_pos));
                foreach (var m in moveSet)
                {
                    if (moveSetLookup.Contains((m.next_pos, m.last_pos))) swapPairs++;
                }
                if (swapPairs > 0)
                {
                    Debug.LogWarning($"[FakeServer] Pre-resolve: detected potential swap pairs x{swapPairs / 2}");
                }
                // Ensure no duplicate movers (same pawn moving twice). Duplicate targets allowed.
                // Compact: compare count vs distinct(last_pos)
                int distinctStarts = moveSet.Select(m => m.last_pos).Distinct().Count();
                if (distinctStarts != moveSet.Count)
                {
                    Debug.LogWarning("[FakeServer] Duplicate movers detected in move set");
                }
                // Diagnostic: list the final move set
                foreach (var mm in moveSet)
                {
                    Debug.Log($"[FakeServer] Pre-resolve move: {mm.last_pos} -> {mm.next_pos}");
                }
                // Diagnostic: occupant info at involved positions
                foreach (var mm in moveSet)
                {
                    if (baseState.pawns.TryGetValue(mm.last_pos, out var occStart))
                    {
                        Debug.Log($"[FakeServer] Occ at start {mm.last_pos}: id={occStart.id} team={occStart.team} rank={occStart.rank} alive={occStart.alive}");
                    }
                    else
                    {
                        Debug.LogWarning($"[FakeServer] No occ at start {mm.last_pos}");
                    }
                    if (baseState.pawns.TryGetValue(mm.next_pos, out var occNext))
                    {
                        Debug.Log($"[FakeServer] Occ at target {mm.next_pos}: id={occNext.id} team={occNext.team} rank={occNext.rank} alive={occNext.alive}");
                    }
                    else
                    {
                        Debug.Log($"[FakeServer] No occ at target {mm.next_pos}");
                    }
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

            // After applying resolve, check game over using same logic as contract (player-agnostic)
            Subphase winner = CheckGameOver(parameters, gameState);

            if (winner != Subphase.Both)
            {
                lobbyInfo.phase = Phase.Finished;
                lobbyInfo.subphase = winner;
                Debug.Log($"[FakeServer] Game over: winner={winner}");
            }
            else { lobbyInfo.phase = Phase.MoveCommit; lobbyInfo.subphase = Subphase.Both; }
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
    
    static Subphase CheckGameOver(LobbyParameters parameters, GameState gameState)
    {
        // Mirrors contract check_game_over: flags, no movable pawns, asymmetric no-legal-moves
        bool hostSurvived = true;
        bool guestSurvived = true;

        // Track dead movable counts and flag deaths
        uint hostDeadMovable = 0;
        uint guestDeadMovable = 0;
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            PawnState pawn = gameState.pawns[i];
            if (!pawn.alive)
            {
                if (pawn.rank.HasValue)
                {
                    Rank r = pawn.rank.Value;
                    bool isHostPawn = (pawn.pawn_id.Value & 1u) == 0u;
                    if (r == Rank.THRONE)
                    {
                        if (isHostPawn) hostSurvived = false; else guestSurvived = false;
                    }
                    else if (r != Rank.TRAP)
                    {
                        if (isHostPawn) hostDeadMovable += 1u; else guestDeadMovable += 1u;
                    }
                }
            }
        }

        // If all movable pawns for a team are dead, that team loses
        uint totalMovableMax = 0u;
        if (parameters.max_ranks != null)
        {
            for (int i = 0; i < parameters.max_ranks.Length; i++)
            {
                Rank rank = (Rank)i;
                if (rank != Rank.THRONE && rank != Rank.TRAP && rank != Rank.UNKNOWN)
                {
                    totalMovableMax += (uint)parameters.max_ranks[i];
                }
            }
        }
        if (totalMovableMax > 0u)
        {
            if (hostDeadMovable >= totalMovableMax) hostSurvived = false;
            if (guestDeadMovable >= totalMovableMax) guestSurvived = false;
        }

        // Early exit if decided by flags/movable counts
        if (!hostSurvived && guestSurvived) return Subphase.Guest;
        if (hostSurvived && !guestSurvived) return Subphase.Host;
        if (!hostSurvived && !guestSurvived) return Subphase.None;

        // Stalemate / blocked: no legal adjacent moves for one side (asymmetric). Skip immovables (THRONE/TRAP) if revealed/known.
        // Build passable map
        var passable = new Dictionary<Vector2Int, bool>();
        if (parameters.board.tiles != null)
        {
            for (int i = 0; i < parameters.board.tiles.Length; i++)
            {
                TileState tile = parameters.board.tiles[i];
                passable[tile.pos] = tile.passable;
            }
        }
        // Build occupancy map for alive pawns
        var occ = new Dictionary<Vector2Int, PawnState>();
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            PawnState ps = gameState.pawns[i];
            if (ps.alive)
            {
                occ[ps.pos] = ps;
            }
        }

        bool hostAnyCanMove = false;
        bool guestAnyCanMove = false;
        uint hostConsideredMovables = 0u;
        uint guestConsideredMovables = 0u;
        bool isHex = parameters.board.hex;

        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            PawnState pawn = gameState.pawns[i];
            if (!pawn.alive) continue;
            bool isHostPawn = (pawn.pawn_id.Value & 1u) == 0u;
            if (pawn.rank.HasValue)
            {
                Rank r = pawn.rank.Value;
                if (r == Rank.THRONE || r == Rank.TRAP) continue;
                if (isHostPawn) hostConsideredMovables += 1u; else guestConsideredMovables += 1u;
            }
            // Neighbor check
            Vector2Int[] neighbors = Shared.GetNeighbors(pawn.pos, isHex);
            bool canMove = false;
            for (int j = 0; j < neighbors.Length; j++)
            {
                Vector2Int next = neighbors[j];
                bool isPassable = passable.TryGetValue(next, out bool p) ? p : false;
                if (!isPassable) continue;
                if (occ.TryGetValue(next, out PawnState occPawn))
                {
                    bool occIsHost = (occPawn.pawn_id.Value & 1u) == 0u;
                    if (occIsHost != isHostPawn) { canMove = true; break; }
                }
                else { canMove = true; break; }
            }
            if (canMove)
            {
                if (isHostPawn) hostAnyCanMove = true; else guestAnyCanMove = true;
                if (hostAnyCanMove && guestAnyCanMove) break;
            }
        }

        if (hostConsideredMovables > 0u && guestConsideredMovables > 0u)
        {
            if (!hostAnyCanMove && guestAnyCanMove) hostSurvived = false;
            if (!guestAnyCanMove && hostAnyCanMove) guestSurvived = false;
        }

        if (!hostSurvived && guestSurvived) return Subphase.Guest;
        if (hostSurvived && !guestSurvived) return Subphase.Host;
        if (!hostSurvived && !guestSurvived) return Subphase.None;
        return Subphase.Both;
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
        if (top_moves == null || top_moves.Count == 0)
        {
            // No legal moves: auto-resign for this side
            bool isHostSide = parameters.host_team == team;
            Resign(isHostSide);
            return new List<HiddenMove>();
        }
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
