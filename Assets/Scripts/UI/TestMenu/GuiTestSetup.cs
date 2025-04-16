using System.Collections.Generic;
using Contract;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestSetup : MonoBehaviour
{
    public Transform rankEntryListRoot;

    public Button clearButton;
    public Button autoSetupButton;
    public Button deleteButton;
    public Button submitButton;
    
    public GameObject rankEntryPrefab;
    MaxPawns[] maxPawns;
    public List<GuiRankListEntry> entries;
    public GuiRankListEntry selectedRankEntry;
    
    public event System.Action<Rank> OnRankSelected;
    
    public void Initialize()
    {
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        if (!maybeLobby.HasValue) return;
        Lobby lobby = maybeLobby.Value;
        maxPawns = lobby.parameters.max_pawns;
        foreach (Transform child in rankEntryListRoot.transform) { Destroy(child.gameObject); }
        selectedRankEntry = null;
        foreach (MaxPawns maxPawn in maxPawns)
        {
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rankListEntry);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
            rankListEntry.Refresh((Rank)maxPawn.rank, maxPawn.max, false);
        }

        // Set up button listeners
        autoSetupButton.onClick.AddListener(OnAutoSetupButton);
    }

    public void Refresh(TestBoardManager boardManager)
    {
        foreach (GuiRankListEntry entry in entries)
        {
            entry.Refresh(entry.rank, entry.remaining, entry.rank == selectedRankEntry?.rank);
        }
    }
    
    public void OnEntryClicked(GuiRankListEntry clickedEntry)
    {
        if (selectedRankEntry == clickedEntry)
        {
            selectedRankEntry = null;
        }
        else
        {
            selectedRankEntry = clickedEntry;
        }
        OnRankSelected?.Invoke(clickedEntry.rank);
    }

    public void OnAutoSetupButton()
    {
        if (GameManager.instance.testBoardManager.currentPhase is SetupTestPhase setupPhase)
        {
            setupPhase.OnAutoSetup();
            GameManager.instance.testBoardManager.UpdateAllPawnVisuals();
        }
    }
}
