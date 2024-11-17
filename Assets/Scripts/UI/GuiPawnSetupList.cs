using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    HashSet<GuiPawnSetupListEntry> entries = new();
    public Transform body;
    public GameObject entryPrefab;
    
    void OnDestroy()
    {
    }

    public void Initialize(SetupParameters setupParameters)
    {
        Debug.Log("GuiPawnSetupList Initialize");
        GameManager.instance.boardManager.OnPawnAdded += OnPawnAdded;
        GameManager.instance.boardManager.OnPawnRemoved += OnPawnRemoved;
        // clear entries
        foreach (var entry in entries)
        {
            Destroy(entry);
        }
        entries.Clear();
        foreach ((PawnDef pawnDef, int maxPawns) in setupParameters.maxPawnsDict)
        {
            GameObject entryObject = Instantiate(entryPrefab, body);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            entry.SetPawn(pawnDef, maxPawns);
            entry.Initialize(pawnDef, maxPawns, OnEntryClicked);
            entries.Add(entry);
        }
        
        // foreach (var kvp in pawnsLeft)
        // {
        //     GameObject entryObject = Instantiate(entryPrefab, body);
        //     GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
        //     entry.SetPawn(kvp.Key, kvp.Value);
        //     entry.OnEntryClicked += OnEntryClicked;
        //     entries.Add(entry);
        // }

        
    }

    void OnPawnAdded(Dictionary<PawnDef, int> pawnsLeft, Pawn addedPawn)
    {
        foreach (var entry in entries)
        {
            entry.SetCount(pawnsLeft[entry.pawnDef]);
        }
    }

    void OnPawnRemoved(Dictionary<PawnDef, int> pawnsLeft, Pawn removedPawn)
    {
        foreach (var entry in entries)
        {
            entry.SetCount(pawnsLeft[entry.pawnDef]);
        }
    }

    void OnEntryClicked(GuiPawnSetupListEntry selectedEntry)
    {
        PawnDef selectedPawnDef = GameManager.instance.boardManager.setupSelectedPawnDef;
        // if entry is already selected, unselect it
        PawnDef newSelectedPawnDef = selectedEntry.pawnDef == selectedPawnDef ? null : selectedEntry.pawnDef;
        GameManager.instance.boardManager.setupSelectedPawnDef = newSelectedPawnDef;
        foreach (var entry in entries)
        {
            entry.SelectEntry(entry.pawnDef == newSelectedPawnDef);
        }
    }

    GuiPawnSetupListEntry GetEntryByPawnDef(PawnDef pawnDef)
    {
        return entries.Where(entry => entry.pawnDef == pawnDef).FirstOrDefault();
    }
}
