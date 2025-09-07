using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardHand : MonoBehaviour
{
    public Card selectedCard;

    public Card hoveredCard;

    public GameObject cardSlotPrefab;
    public GameObject cardPrefab;
    public int cardsToSpawn = 7;
    public List<CardSlot> slots;
    public List<Card> cards;

    public bool isCrossing = false;

    public bool tweenCardReturn = true;

    public RectTransform rect;

    public Vector3 slotEulerOffset = Vector3.zero;
    public float slotZOffsetPerIndex = 0.001f;

    public Transform topLeft;
    public Transform topRight;
    public Transform botLeft;
    public Transform botRight;
    
    void Start()
    {
        EnsureLists();
        AddCards(cardsToSpawn);
        UpdateRectFromCorners();
        UpdateSlotsAlongRect();
    }


    void Update()
    {
        UpdateSlotsAlongRect();
    }

    void UpdateRectFromCorners()
    {
        if (rect == null || topLeft == null || topRight == null || botLeft == null || botRight == null)
        {
            return;
        }

        Vector3 pTL = topLeft.position;
        Vector3 pTR = topRight.position;
        Vector3 pBL = botLeft.position;
        Vector3 pBR = botRight.position;

        Vector3 xAxis = (pTR - pTL);
        Vector3 yAxis = (pTL - pBL);

        float widthWorld = xAxis.magnitude;
        float heightWorld = yAxis.magnitude;

        if (widthWorld <= Mathf.Epsilon || heightWorld <= Mathf.Epsilon)
        {
            return;
        }

        xAxis.Normalize();
        yAxis.Normalize();

        Vector3 center = (pTL + pTR + pBL + pBR) * 0.25f;
        Vector3 forward = Vector3.Cross(xAxis, yAxis);
        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        rect.position = center;
        rect.rotation = Quaternion.LookRotation(forward, yAxis);

        // Convert world size to RectTransform size (accounts for lossyScale)
        Vector3 lossy = rect.lossyScale;
        float sizeX = lossy.x != 0f ? widthWorld / lossy.x : widthWorld;
        float sizeY = lossy.y != 0f ? heightWorld / lossy.y : heightWorld;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sizeX);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizeY);
    }

    void UpdateSlotsAlongRect()
    {
        if (rect == null || slots == null || slots.Count == 0)
        {
            return;
        }

        float widthWorld = rect.rect.width * rect.lossyScale.x;
        float heightWorld = rect.rect.height * rect.lossyScale.y;

        bool alongWidth = widthWorld >= heightWorld;
        Vector3 axis = alongWidth ? rect.right : rect.up;
        float length = alongWidth ? widthWorld : heightWorld;
        Vector3 center = rect.position;
        Quaternion slotRotation = rect.rotation * Quaternion.Euler(slotEulerOffset);

        int n = slots.Count;
        if (n == 1)
        {
            slots[0].transform.position = center;
            slots[0].transform.rotation = slotRotation;
            return;
        }

        // Distribute centers with equal margins on both ends
        float spacing = length / n;
        float start = (-length * 0.5f) + (spacing * 0.5f);
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = center + axis * (start + spacing * i);
            Vector3 depthOffset = rect.forward * (slotZOffsetPerIndex * i);
            if (slots[i] != null)
            {
                slots[i].transform.position = pos + depthOffset;
                slots[i].transform.rotation = slotRotation;
            }
        }
    }

	void EnsureLists()
	{
		if (slots == null) slots = new List<CardSlot>();
		if (cards == null) cards = new List<Card>();
	}

	public Card AddCard()
	{
		EnsureLists();
		Transform parent = rect != null ? rect.transform : transform;
		var slotObject = Instantiate(cardSlotPrefab, parent);
		CardSlot slot = slotObject.GetComponent<CardSlot>();
		slots.Add(slot);
		var cardObject = Instantiate(cardPrefab, slotObject.transform);
		Card card = cardObject.GetComponent<Card>();
		slot.card = card;
		cards.Add(card);
		card.Initialize(0);
		UpdateSlotsAlongRect();
		return card;
	}

	public void AddCards(int count)
	{
		for (int i = 0; i < count; i++)
		{
			AddCard();
		}
	}

	public void RemoveCard(Card card)
	{
		if (card == null) return;
		int index = -1;
		for (int i = 0; i < slots.Count; i++)
		{
			if (slots[i] != null && slots[i].card == card)
			{
				index = i;
				break;
			}
		}
		if (index >= 0)
		{
			RemoveAt(index);
		}
	}

	public void RemoveAt(int index)
	{
		EnsureLists();
		if (index < 0 || index >= slots.Count) return;
		CardSlot slot = slots[index];
		Card card = slot != null ? slot.card : null;
		if (card != null)
		{
			cards.Remove(card);
			Destroy(card.gameObject);
		}
		slots.RemoveAt(index);
		if (slot != null)
		{
			Destroy(slot.gameObject);
		}
		UpdateSlotsAlongRect();
	}
}
