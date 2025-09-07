using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuiGallery : MenuElement
{
    public Button prevButton;

    public Button nextButton;

    public Button backButton;

    public Card currentCard;

    public List<Card> cards;

    public List<GameObject> cardPrefabs;
    public List<Transform> slots;
    public int index = 0;
    public Transform currentCardParent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentCard = Instantiate(cardPrefabs[index], currentCardParent).GetComponent<Card>();
        currentCard.rotation.enabled = true;
        prevButton.onClick.AddListener(OnPrevButton);
        nextButton.onClick.AddListener(OnNextButton);
    }

    void OnNextButton()
    {
        if (index < cardPrefabs.Count - 1)
        {
            var oldPivot = currentCard.pivot.rotation;
            Destroy(currentCard.gameObject);
            index++;
            currentCard = Instantiate(cardPrefabs[index], currentCardParent).GetComponent<Card>();
            currentCard.rotation.enabled = true;
            currentCard.pivot.rotation = oldPivot;

        }
    }

    void OnPrevButton()
    {
        if (index > 0)
        {
            var oldPivot = currentCard.pivot.rotation;
            Destroy(currentCard.gameObject);
            index--;
            currentCard = Instantiate(cardPrefabs[index], currentCardParent).GetComponent<Card>();
            currentCard.rotation.enabled = true;
            currentCard.pivot.rotation = oldPivot;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public override void Refresh()
    {
        
    }
    
}
