using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    public GuiPawnSetup master;
    public Transform body;
    public GameObject entryPrefab;
    HashSet<GuiPawnSetupListEntry> entries;
    
    public void Initialize(GuiPawnSetup inMaster, SSetupParameters setupParameters)
    {
        master = inMaster;
        GameManager.instance.boardManager.OnSetupStateChanged += UpdateState;
        entries ??= new HashSet<GuiPawnSetupListEntry>();
        foreach (GuiPawnSetupListEntry entry in entries)
        {
            Destroy(entry.gameObject);
        }
        entries = new HashSet<GuiPawnSetupListEntry>();
        foreach (SMaxPawnsPerRank maxPawns in setupParameters.maxPawns)
        {
            // TODO: make this care about rank instead of pawnDef so we can support multiple pawnDef
            GameObject entryObject = Instantiate(entryPrefab, body);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            PawnDef pawnDef = GameManager.instance.orderedPawnDefList.FirstOrDefault(def => def.rank == maxPawns.rank);
            entry.SetPawn(pawnDef, maxPawns.max);
            entry.Initialize(this, pawnDef, maxPawns.max);
            entries.Add(entry);
        }
        UpdateState(null);
    }

    
    public void OnEntryClicked(GuiPawnSetupListEntry inSelectedEntry)
    {
        PawnDef pawnDef = inSelectedEntry == null ? null : inSelectedEntry.pawnDef;
        master.OnSetupPawnDefSelected(pawnDef);
    }
    
    void UpdateState(PawnDef selectedPawnDef)
    {
        Player currentPlayer = GameManager.instance.boardManager.player;
        // Get all PawnViews from the BoardManager
        List<PawnView> pawnViews = GameManager.instance.boardManager.pawnViews;
        foreach (GuiPawnSetupListEntry entry in entries)
        {
            entry.SelectEntry(entry.pawnDef == selectedPawnDef);
            // Count the number of alive pawns of this PawnDef for the current player
            int numAlivePawns = pawnViews.Count(pawnView =>
                pawnView.pawn.def == entry.pawnDef &&
                pawnView.pawn.player == currentPlayer &&
                pawnView.pawn.isAlive);
            int remainingPawns = entry.maxPawns - numAlivePawns;
            entry.SetCount(remainingPawns);
        }
    }
}
