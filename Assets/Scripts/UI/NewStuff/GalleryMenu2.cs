using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using TMPro;

public class GalleryMenu2 : MenuBase
{
    public Button backButton;

    public GameObject cardInfoContainer;

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    public Card currentCard;

    public Dictionary<(Team, Rank),Card> cards;
    Dictionary<Card, (Team team, Rank rank, PawnDef def)> cardInfo = new();

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
        cardInfo.Clear();
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
                GameObject cardPrefabRed = Instantiate(pawnDef.redCard, galleryEnvironment.transform);
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
                cardInfo[cardRed] = (Team.RED, pawnDef.rank, pawnDef);
                cardInfo[cardBlue] = (Team.BLUE, pawnDef.rank, pawnDef);
            }
            PlayOpenAnimation();
            UpdateTexts(null);
            if (cardInfoContainer != null) cardInfoContainer.SetActive(false);
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

        ApplySelection(card);
    }

    void ApplySelection(Card clicked)
    {
        // Toggle off if clicking the current card
        if (currentCard == clicked)
        {
            RestoreAndDeselect(clicked);
            currentCard = null;
            UpdateTexts(null);
            if (cardInfoContainer != null) cardInfoContainer.SetActive(false);
            return;
        }

        // Deselect and restore the previous card
        if (currentCard != null)
        {
            RestoreAndDeselect(currentCard);
            currentCard = null;
        }

        // Select the new card
        if (clicked != null)
        {
            SelectCard(clicked);
        }
    }

    void RestoreAndDeselect(Card card)
    {
        if (card == null) return;
        if (originalSlotByCard.TryGetValue(card, out Transform original))
        {
            card.SetSlot(original);
            originalSlotByCard.Remove(card);
        }
        card.SetSelected(false);
        card.SetRotationEnabled(false);
        // Deselected → wobble on
        card.SetWobbleEnabled(true);
    }

    void SelectCard(Card card)
    {
        if (card == null) return;
        originalSlotByCard[card] = card.slot;
        currentCard = card;
        currentCard.SetSelected(true);
        currentCard.SetSlot(galleryEnvironment.frontSlot);
        currentCard.SetRotationEnabled(true);
        // Selected card → wobble off
        currentCard.SetWobbleEnabled(false);
        UpdateTexts(currentCard);
        if (cardInfoContainer != null) cardInfoContainer.SetActive(true);
    }

    void UpdateTexts(Card selected)
    {
        if (nameText == null || descriptionText == null)
        {
            return;
        }
        if (selected == null)
        {
            nameText.text = string.Empty;
            descriptionText.text = string.Empty;
            return;
        }
        if (cardInfo.TryGetValue(selected, out var info))
        {
			if (info.team == Team.RED)
			{
				nameText.text = $"{info.rank} - {info.def.redName ?? string.Empty}";
				descriptionText.text = info.def.redDescription ?? string.Empty;
			}
			else
			{
				nameText.text = $"{info.rank} - {info.def.blueName ?? string.Empty}";
				descriptionText.text = info.def.blueDescription ?? string.Empty;
			}
        }
        else
        {
            nameText.text = string.Empty;
            descriptionText.text = string.Empty;
        }
    }
}


