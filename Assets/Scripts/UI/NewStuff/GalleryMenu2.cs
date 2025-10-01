using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GalleryMenu2 : MenuBase
{
    public Button backButton;

    public Button teamButton;
    public Button prevRankButton;
    public Button nextRankButton;

    public Card currentCard;

    public List<Card> cards;

    public Transform cardRoot;
    
    public Team currentTeam;
    public Rank currentRank;

    
    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
    }

    public void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }
    public override void Refresh()
    {
    }
}


