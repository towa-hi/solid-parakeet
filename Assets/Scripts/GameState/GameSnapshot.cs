using System.Collections.Generic;
using Contract;
using UnityEngine;

public record GameSnapshot
{
    // Authoritative snapshot from network
    public GameNetworkState Net { get; init; }
    // Derived client mode
    public ClientMode Mode { get; init; }
    // Local per-mode UI state
    public LocalUiState Ui { get; init; }
    // Placeholder for expansion during migration
    public static GameSnapshot Empty => new GameSnapshot { Ui = LocalUiState.Empty };

	public bool TryBuildCommitSetupReq(out CommitSetupReq req)
	{
		if (!Net.IsMySubphase())
		{
			req = default;
			return false;
		}
		var pending = Ui?.PendingCommits ?? new Dictionary<PawnId, Rank?>();
		// Require a complete setup: the number of placed ranks must equal the sum of max_ranks
		uint[] maxRanks = Net.lobbyParameters.max_ranks;
		int requiredCount = 0;
		for (int i = 0; i < maxRanks.Length; i++) requiredCount += (int)maxRanks[i];
		int placedCount = 0;
		foreach (var v in pending.Values) if (v.HasValue) placedCount++;
		if (placedCount != requiredCount)
		{
			req = default;
			return false;
		}
		List<HiddenRank> hiddenRanks = new();
		List<SetupCommit> commits = new();
		foreach (var kv in pending)
		{
			PawnId pawnId = kv.Key;
			Rank? maybeRank = kv.Value;
			if (maybeRank is not Rank rank)
				continue; // skip uncommitted
			HiddenRank hiddenRank = new()
			{
				pawn_id = pawnId,
				rank = rank,
				salt = Globals.RandomSalt(),
			};
			hiddenRanks.Add(hiddenRank);
			commits.Add(new SetupCommit
			{
				pawn_id = pawnId,
				hidden_rank_hash = SCUtility.Get16ByteHash(hiddenRank),
			});
		}
		if (commits.Count == 0)
		{
			req = default;
			return false;
		}
		List<byte[]> leaves = new();
		foreach (var c in commits) leaves.Add(c.hidden_rank_hash);
		(byte[] root, MerkleTree tree) = MerkleTree.BuildMerkleTree(leaves.ToArray());
		if (Net.lobbyParameters.security_mode)
		{
			// Cache proofs for later prove-rank
			List<CachedRankProof> proofs = new();
			for (int i = 0; i < commits.Count; i++)
			{
				HiddenRank hiddenRank = hiddenRanks[i];
				MerkleProof proof = tree.GenerateProof((uint)i);
				proofs.Add(new CachedRankProof { hidden_rank = hiddenRank, merkle_proof = proof });
			}
			CacheManager.StoreHiddenRanksAndProofs(proofs, Net.address, Net.lobbyInfo.index);
		}
		req = new CommitSetupReq
		{
			lobby_id = Net.lobbyInfo.index,
			rank_commitment_root = root,
			zz_hidden_ranks = Net.lobbyParameters.security_mode ? new HiddenRank[]{} : hiddenRanks.ToArray(),
		};
		return true;
	}

	public bool TryBuildCommitMoveAndProveMoveReqs(out CommitMoveReq commit, out ProveMoveReq prove)
	{
		commit = default;
		prove = default;
		if (!Net.IsMySubphase())
		{
			return false;
		}
		var pairs = Ui?.MovePairs ?? new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>();
		if (pairs.Count == 0)
		{
			return false;
		}
		List<HiddenMove> hiddenMoves = new();
		List<byte[]> moveHashes = new();
		foreach (var kv in pairs)
		{
			PawnId pawnId = kv.Key;
			Vector2Int start = kv.Value.start;
			Vector2Int target = kv.Value.target;
			// Diagnostics: verify that at submit time the start tile still holds this pawn id
			var occ = Net.GetAlivePawnFromPosChecked(start);
			if (occ is PawnState occPawn && occPawn.pawn_id != pawnId)
			{
				Debug.LogWarning($"GameSnapshot: BuildMoveReqs mismatch at start. start={start} target={target} expectedId={pawnId} nowId={occPawn.pawn_id}");
			}
			else if (occ is null)
			{
				Debug.LogWarning($"GameSnapshot: BuildMoveReqs no occupant at start. start={start} target={target} expectedId={pawnId}");
			}
			HiddenMove hm = new()
			{
				pawn_id = pawnId,
				salt = Globals.RandomSalt(),
				start_pos = start,
				target_pos = target,
			};
			// Cache hidden move for later auto-prove
			CacheManager.StoreHiddenMove(hm, Net.address, Net.lobbyInfo.index);
			hiddenMoves.Add(hm);
			moveHashes.Add(SCUtility.Get16ByteHash(hm));
		}
		commit = new CommitMoveReq
		{
			lobby_id = Net.lobbyInfo.index,
			move_hashes = moveHashes.ToArray(),
		};
		prove = new ProveMoveReq
		{
			lobby_id = Net.lobbyInfo.index,
			move_proofs = hiddenMoves.ToArray(),
		};
		return moveHashes.Count > 0;
	}

	public bool TryBuildProveMoveReqFromCache(out ProveMoveReq req)
	{
		if (Net.lobbyInfo.phase != Phase.MoveProve || !Net.IsMySubphase())
		{
			req = default;
			return false;
		}
		var hashes = Net.GetUserMove().move_hashes;
		if (hashes == null || hashes.Length == 0)
		{
			req = default;
			return false;
		}
		List<HiddenMove> hiddenMoves = new();
		foreach (var h in hashes)
		{
			var maybe = CacheManager.GetHiddenMove(h);
			if (maybe is HiddenMove hm)
			{
				hiddenMoves.Add(hm);
			}
			else
			{
				Debug.LogWarning("TryBuildProveMoveReqFromCache: missing hidden move for a committed hash");
				req = default;
				return false;
			}
		}
		req = new ProveMoveReq
		{
			lobby_id = Net.lobbyInfo.index,
			move_proofs = hiddenMoves.ToArray(),
		};
		return true;
	}

	public bool TryBuildProveRankReqFromCache(out ProveRankReq req)
	{
		if (Net.lobbyInfo.phase != Phase.RankProve || !Net.IsMySubphase())
		{
			req = default;
			return false;
		}
		var needed = Net.GetUserMove().needed_rank_proofs;
		if (needed == null || needed.Length == 0)
		{
			req = default;
			return false;
		}
		List<HiddenRank> hiddenRanks = new();
		List<MerkleProof> merkleProofs = new();
		foreach (PawnId pawnId in needed)
		{
			if (CacheManager.GetHiddenRankAndProof(pawnId) is not CachedRankProof cached)
			{
				Debug.LogWarning($"TryBuildProveRankReqFromCache: missing cached rank/proof for pawn {pawnId}");
				req = default;
				return false;
			}
			hiddenRanks.Add(cached.hidden_rank);
			merkleProofs.Add(cached.merkle_proof);
		}
		req = new ProveRankReq
		{
			lobby_id = Net.lobbyInfo.index,
			hidden_ranks = hiddenRanks.ToArray(),
			merkle_proofs = merkleProofs.ToArray(),
		};
		return true;
	}

	public bool TryBuildTileTooltip(Vector2Int tilePos, out string header, out string body)
	{
		header = $"{tilePos}";
		body = string.Empty;

		GameNetworkState net = Net;
		PawnState[] pawns = net.gameState.pawns;
		bool hasNetworkData = pawns != null && pawns.Length > 0;
		if (!hasNetworkData)
		{
			return !string.IsNullOrEmpty(header) || !string.IsNullOrEmpty(body);
		}

		Rank rank = Rank.UNKNOWN;

		switch (Mode)
		{
			case ClientMode.Setup:
			{
				var pawn = net.GetAlivePawnFromPosChecked(tilePos);
				if (pawn.HasValue)
				{
					var pending = Ui?.PendingCommits;
					if (pending != null && pending.TryGetValue(pawn.Value.pawn_id, out Rank? maybe) && maybe.HasValue)
					{
						rank = maybe.Value;
					}
				}
				break;
			}
			case ClientMode.Resolve:
			{
				if (Ui != null)
				{
					PawnState? pawn = Ui.ResolveData.GetPawnAt(tilePos, Ui.Checkpoint, Ui.BattleIndex);
					if (!pawn.HasValue && Ui.Checkpoint == ResolveCheckpoint.Final)
					{
						pawn = net.GetAlivePawnFromPosChecked(tilePos);
					}
					if (pawn.HasValue)
					{
						var known = pawn.Value.GetKnownRank(net.userTeam);
						if (known.HasValue)
						{
							rank = known.Value;
						}
					}
				}
				break;
			}
			default:
			{
				var pawn = net.GetAlivePawnFromPosChecked(tilePos);
				if (pawn.HasValue)
				{
					var known = pawn.Value.GetKnownRank(net.userTeam);
					if (known.HasValue)
					{
						rank = known.Value;
					}
				}
				break;
			}
		}

		if (rank != Rank.UNKNOWN)
		{
			string power = $"Power: {(int)rank}";
			if (Mode == ClientMode.Setup)
			{
				int committed = 0;
				int max = 0;
				var pending = Ui?.PendingCommits;
				if (pending != null)
				{
					foreach (var v in pending.Values)
					{
						if (v.HasValue && v.Value == rank)
						{
							committed++;
						}
					}
				}
				uint[] maxRanks = net.lobbyParameters.max_ranks;
				int rankIndex = (int)rank;
				if (maxRanks != null && rankIndex >= 0 && rankIndex < maxRanks.Length)
				{
					max = (int)maxRanks[rankIndex];
				}
				body = $"{rank} {power}\nCommitted: {committed}/{max}";
			}
			else
			{
				body = $"{rank} {power}";
			}
		}

		return !string.IsNullOrEmpty(header) || !string.IsNullOrEmpty(body);
	}
}


