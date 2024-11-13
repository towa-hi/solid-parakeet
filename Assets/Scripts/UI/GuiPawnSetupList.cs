using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    HashSet<GuiPawnSetupListEntry> entries = new();
    public GameObject body;
    public GameObject entryPrefab;

    public void Initialize(Dictionary<PawnDef, int> pawnsLeft)
    {
        foreach (var entry in entries)
        {
            Destroy(entry);
        }
        entries.Clear();
        foreach (var kvp in pawnsLeft)
        {
            GameObject entryObject = Instantiate(entryPrefab, body.transform);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            entry.SetPawn(kvp.Key, kvp.Value);
            entry.OnEntryClicked += OnEntryClicked;
            entries.Add(entry);
        }

        GameManager.instance.boardManager.OnPawnAdded += OnPawnAdded;
        GameManager.instance.boardManager.OnPawnRemoved += OnPawnRemoved;
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
