using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    public Transform body;
    public GameObject entryPrefab;
    HashSet<GuiPawnSetupListEntry> entries;
    public GuiPawnSetupListEntry selectedEntry;
    
    public void Initialize(SetupParameters setupParameters)
    {
        //Debug.Log("GuiPawnSetupList: Initialize");
        selectedEntry = null;
        entries ??= new HashSet<GuiPawnSetupListEntry>();
        foreach (GuiPawnSetupListEntry entry in entries)
        {
            Destroy(entry);
        }
        entries = new HashSet<GuiPawnSetupListEntry>();
        foreach ((PawnDef pawnDef, int maxPawns) in setupParameters.maxPawnsDict)
        {
            GameObject entryObject = Instantiate(entryPrefab, body);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            entry.SetPawn(pawnDef, maxPawns);
            entry.Initialize(pawnDef, maxPawns, OnEntryClicked);
            entries.Add(entry);
        }
    }
    
    public void UpdateList(List<SetupPawnView> setupPawnViews)
    {
        Player currentPlayer = GameManager.instance.boardManager.player;
        foreach (GuiPawnSetupListEntry entry in entries)
        {
            int numPlacedPawns = setupPawnViews.Count(pawnView =>
                pawnView.pawn.def == entry.pawnDef && pawnView.pawn.player == currentPlayer);
            int remainingPawns = entry.maxPawns - numPlacedPawns;
            entry.SetCount(remainingPawns);
        }
    }
    
    void OnEntryClicked(GuiPawnSetupListEntry inSelectedEntry)
    {
        if (selectedEntry != null && selectedEntry != inSelectedEntry)
        {
            selectedEntry.SelectEntry(false);
        }
        if (selectedEntry == inSelectedEntry)
        {
            selectedEntry = null;
            inSelectedEntry.SelectEntry(false);
        }
        else
        {
            selectedEntry = inSelectedEntry;
            selectedEntry.SelectEntry(true);
        }
        PawnDef pawnDef = selectedEntry == null ? null : selectedEntry.pawnDef;
        GameManager.instance.boardManager.OnSetupPawnEntrySelected(pawnDef);
    }
}
