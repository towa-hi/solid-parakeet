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
    public bool initialized; 
    
    public void Initialize(SetupParameters setupParameters)
    {
        //Debug.Log("GuiPawnSetupList: Initialize");
        selectedEntry = null;
        entries ??= new HashSet<GuiPawnSetupListEntry>();
        foreach (GuiPawnSetupListEntry entry in entries)
        {
            Destroy(entry.gameObject);
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

        GameManager.instance.boardManager.OnPawnModified += OnPawnModified;
        initialized = true;
        UpdateList();
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

    void OnPawnModified(PawnChanges pawnChanges)
    {
        if (pawnChanges.pawn.player == GameManager.instance.boardManager.player)
        {
            UpdateList();
        }
    }
    
    public void UpdateList()
    {
        if (!initialized)
            return;

        Player currentPlayer = GameManager.instance.boardManager.player;

        // Get all PawnViews from the BoardManager
        List<PawnView> pawnViews = GameManager.instance.boardManager.pawnViews;

        foreach (GuiPawnSetupListEntry entry in entries)
        {
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
