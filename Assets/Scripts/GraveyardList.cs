using UnityEngine;
using System.Collections.Generic;
using Contract;

public class GraveyardList : MonoBehaviour
{
    public Transform entryListRoot;
    public GameObject entryPrefab;
    public List<GraveyardListEntry> entries;

    void Awake()
    {
        if (entries == null)
        {
            entries = new();
        }
    }

    void Start()
    {
        entries = new();
    }

    public void Clear()
    {
        if (entryListRoot != null)
        {
            for (int i = entryListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(entryListRoot.GetChild(i).gameObject);
            }
        }
        if (entries == null) entries = new();
        else entries.Clear();
    }
    public void Refresh(GameNetworkState netState)
    {
        Debug.Log($"GraveyardList.Refresh: netState={netState}");
        Clear();
        entries = new();
        PawnState[] pawns = netState.gameState.pawns;
        for (int i = 0; i < netState.lobbyParameters.max_ranks.Length; i++)
        {
            Rank rank = (Rank)i;
            if (rank == Rank.UNKNOWN)
            {
                continue;
            }
            int max = (int)netState.lobbyParameters.max_ranks[i];
            // get red and blue alive counts
            int redAlive = 0;
            int blueAlive = 0;
            foreach (PawnState pawn in pawns)
            {
                if (pawn.rank == rank && pawn.alive)
                {
                    if (pawn.GetTeam() == Team.RED)
                    {
                        redAlive++;
                    }
                    else
                    {
                        blueAlive++;
                    }
                }
            }
            GameObject entry = Instantiate(entryPrefab, entryListRoot);
            GraveyardListEntry entryComponent = entry.GetComponent<GraveyardListEntry>();
            entryComponent.Set(rank, redAlive, blueAlive, max);
            entries.Add(entryComponent);
        }
    }
}
