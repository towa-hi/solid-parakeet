using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuiPawnSetupList : MonoBehaviour
{
    HashSet<GuiPawnSetupListEntry> entries = new();
    public GameObject body;
    public GameObject entryPrefab;

    public void Initialize(SetupParameters setupParameters)
    {
        foreach (var kvp in setupParameters.maxPawnsList)
        {
            GameObject entryObject = Instantiate(entryPrefab, body.transform);
            GuiPawnSetupListEntry entry = entryObject.GetComponent<GuiPawnSetupListEntry>();
            entry.SetPawn(kvp.Key, kvp.Value);
            entries.Add(entry);
        }

        GameManager.instance.boardManager.OnPawnAdded += OnPawnAdded;
        GameManager.instance.boardManager.OnPawnRemoved += OnPawnRemoved;
    }

    void OnPawnAdded(PawnView addedPawnView)
    {
        foreach (GuiPawnSetupListEntry entry in entries.Where(entry => entry.pawnDef == addedPawnView.pawn.def))
        {
            entry.DecrementCount();
        }
    }

    void OnPawnRemoved(PawnView removedPawnView)
    {
        foreach (GuiPawnSetupListEntry entry in entries.Where(entry => entry.pawnDef == removedPawnView.pawn.def))
        {
            entry.IncrementCount();
        }
    }
}
