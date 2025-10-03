using UnityEngine;
using System.Collections.Generic;
using Contract;

public class Graveyard : MonoBehaviour
{
    public GraveyardEntry entryPrefab;
    public List<Transform> entrySlots; // set manually in inspector, at least 20
    public List<GraveyardEntry> entries; 
    // even index is RED, odd index is BLUE
    // throne and unknown are skipped
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Initialize(GameNetworkState netState)
    {
        foreach (GraveyardEntry entry in entries)
        {
            Destroy(entry.gameObject);
        }
        Debug.Assert(entrySlots.Count == 20, "entrySlots.Count == 20");
        entries.Clear();
        for (int i = 0; i < netState.lobbyParameters.max_ranks.Length; i += 2)
        {
            Rank rank = (Rank)i;
            if (rank == Rank.THRONE || rank == Rank.UNKNOWN)
            {
                continue;
            }
            if (netState.lobbyParameters.max_ranks[i] == 0)
            {
                continue;
            }
            GraveyardEntry redEntry = Instantiate(entryPrefab, entrySlots[i]);
            GraveyardEntry blueEntry = Instantiate(entryPrefab, entrySlots[i + 1]);
            redEntry.Initialize(Team.RED, rank, netState.lobbyParameters.max_ranks[i], 0);
            blueEntry.Initialize(Team.BLUE, rank, netState.lobbyParameters.max_ranks[i], 0);
            entries.Add(redEntry);
            entries.Add(blueEntry);
        }
    }

    public void AttachSubscriptions()
    {
        // ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChanged;
        // ViewEventBus.OnResolveCheckpointChanged += HandleResolveCheckpointChanged;
    }

    public void DetachSubscriptions()
    {
        // ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChanged;
    }

    public void SeedFromSnapshot(PawnState[] snapshot)
    {
        ApplyAliveSnapshot(snapshot);
    }

    public void SeedFromUi(GameNetworkState net, LocalUiState ui)
    {
        PawnState[] snapshot = null;
        switch (ui.Checkpoint)
        {
            case ResolveCheckpoint.Final:
                snapshot = ui.ResolveData.finalSnapshot ?? net.gameState.pawns;
                break;
            default:
                snapshot = ui.ResolveData.preSnapshot ?? net.gameState.pawns;
                break;
        }
        ApplyAliveSnapshot(snapshot);
    }

    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        Debug.Log($"[Graveyard] Begin Resolve checkpoint={checkpoint} idx={battleIndex}");
        PawnState[] snapshot = checkpoint == ResolveCheckpoint.Final ? tr.finalSnapshot : tr.preSnapshot;
        ApplyAliveSnapshot(snapshot);
        Debug.Log($"[Graveyard] End Resolve checkpoint={checkpoint} idx={battleIndex}");
    }

    void ApplyAliveSnapshot(PawnState[] snapshot)
    {
        if (snapshot == null)
        {
            return;
        }
        Dictionary<(Team, Rank), uint> aliveMap = BuildAliveMap(snapshot);
        for (int i = 0; i < entries.Count; i++)
        {
            GraveyardEntry e = entries[i];
            if (aliveMap.TryGetValue((e.team, e.rank), out uint alive))
            {
                e.Refresh(alive);
            }
        }
    }

    static Dictionary<(Team, Rank), uint> BuildAliveMap(PawnState[] snapshot)
    {
        Dictionary<(Team, Rank), uint> map = new Dictionary<(Team, Rank), uint>();
        for (int i = 0; i < snapshot.Length; i++)
        {
            PawnState p = snapshot[i];
            if (!p.alive)
            {
                continue;
            }
            if (!p.rank.HasValue)
            {
                continue;
            }
            Rank rank = p.rank.Value;
            if (rank == Rank.THRONE || rank == Rank.UNKNOWN)
            {
                continue;
            }
            Team team = p.GetTeam();
            (Team, Rank) key = (team, rank);
            if (!map.ContainsKey(key))
            {
                map[key] = 0;
            }
            map[key] = map[key] + 1u;
        }
        return map;
    }
}
