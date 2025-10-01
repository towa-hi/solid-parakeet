using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class GalleryMenu2 : MenuBase
{
    public Button backButton;

    public Button teamButton;
    public Button prevRankButton;
    public Button nextRankButton;

    public Card currentCard;

    public Dictionary<(Team, Rank),Card> cards;

    public GalleryEnvironment galleryEnvironment;

    public Team currentTeam;
    public Rank currentRank;

    // Remember each card's original slot when selected so we can restore it
    Dictionary<Card, Transform> originalSlotByCard = new();

    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
        OnOpened += OnGalleryOpened;
        cards = new();

    }

    public void Initialize(GalleryEnvironment galleryEnvironment)
    {
        this.galleryEnvironment = galleryEnvironment;
    }

    void OnDestroy()
    {
        OnOpened -= OnGalleryOpened;
        foreach (var card in cards)
        {
            Destroy(card.Value.gameObject);
        }
        cards.Clear();
    }

    public void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }
    public override void Refresh()
    {
    }
    
    public void OnGalleryOpened()
    {
        try {
            PawnDef[] pawnDefs = ResourceRoot.OrderedPawnDefs.ToArray();
            foreach (PawnDef pawnDef in pawnDefs)
            {
                if (pawnDef.rank == Rank.UNKNOWN)
                {
                    continue;
                }
                Debug.Log($"Spawning card for red rank {pawnDef.rank}");
                GameObject cardPrefabRed = Instantiate(pawnDef.redCard, galleryEnvironment.transform);
                Debug.Log($"Spawning card for blue rank {pawnDef.rank}");
                GameObject cardPrefabBlue = Instantiate(pawnDef.blueCard, galleryEnvironment.transform);
                cardPrefabRed.transform.position = galleryEnvironment.redCardOrigin.position;
                cardPrefabBlue.transform.position = galleryEnvironment.blueCardOrigin.position;
                Card cardRed = cardPrefabRed.GetComponent<Card>();
                Card cardBlue = cardPrefabBlue.GetComponent<Card>();
                cardRed.Initialize(0.7f);
                cardBlue.Initialize(0.7f);
                cardRed.SetSlot(galleryEnvironment.redCardOrigin);
                cardBlue.SetSlot(galleryEnvironment.blueCardOrigin);
                // Ensure CardRotation starts disabled; unselected cards wobble by default
                cardRed.SetRotationEnabled(false);
                cardBlue.SetRotationEnabled(false);
                cardRed.SetWobbleEnabled(true);
                cardBlue.SetWobbleEnabled(true);
                // Wire click handlers
                cardRed.Clicked += HandleCardClicked;
                cardBlue.Clicked += HandleCardClicked;
                cards.Add((Team.RED, pawnDef.rank), cardRed);
                cards.Add((Team.BLUE, pawnDef.rank), cardBlue);
            }
            PlayOpenAnimation();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnGalleryOpened: {e}");
        }
        
    }

    public void PlayOpenAnimation()
    {
        StartCoroutine(AnimationInitialLayout());
    }

    System.Collections.IEnumerator AnimationInitialLayout()
    {
        foreach (Rank rank in Rank.GetValues(typeof(Rank)))
        {
            if (rank == Rank.UNKNOWN) continue;
            Card cardRed = cards[(Team.RED, rank)];
            Card cardBlue = cards[(Team.BLUE, rank)];
            cardRed.SetSlot(galleryEnvironment.redSlots[(int)rank]);
            cardBlue.SetSlot(galleryEnvironment.blueSlots[(int)rank]);
            yield return new WaitForSeconds(0.1f);
        }
    }

    void HandleCardClicked(Card card)
    {
        if (galleryEnvironment == null || galleryEnvironment.frontSlot == null)
        {
            Debug.LogWarning("GalleryMenu2: frontSlot is not configured in GalleryEnvironment.");
            return;
        }

        // If clicking the currently selected card, restore it
        if (currentCard == card)
        {
            if (originalSlotByCard.TryGetValue(card, out Transform original))
            {
                card.SetSelected(false);
                card.SetSlot(original);
                originalSlotByCard.Remove(card);
                card.SetRotationEnabled(false);
                // Deselected → wobble on
                card.SetWobbleEnabled(true);
            }
            currentCard = null;
            return;
        }

        // If another card is selected, restore it first
        if (currentCard != null)
        {
            if (originalSlotByCard.TryGetValue(currentCard, out Transform prevOriginal))
            {
                currentCard.SetSelected(false);
                currentCard.SetSlot(prevOriginal);
                originalSlotByCard.Remove(currentCard);
                currentCard.SetRotationEnabled(false);
                // Previous now unselected → wobble on
                currentCard.SetWobbleEnabled(true);
            }
            currentCard = null;
        }

        // Select the new card: remember its slot, move to front, mark selected
        if (card != null)
        {
            originalSlotByCard[card] = card.slot;
            currentCard = card;
            currentCard.SetSelected(true);
            currentCard.SetSlot(galleryEnvironment.frontSlot);
            currentCard.SetRotationEnabled(true);
            // Selected card → wobble off
            currentCard.SetWobbleEnabled(false);
        }
    }
}


