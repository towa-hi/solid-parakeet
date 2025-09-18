using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SetupScreen : MonoBehaviour
{
    // this component is responsible for populating the 3d UI that is attached
    // to the camera. it looks like a hand of cards.
    // one card for each rank in maxranks. clicking the card selects the rank.
    // when a card is selected, it is pushed up in the hand.
    // when all pawns of that rank are exhausted, the card is grayed out.
    // this is a replacement for the list based setup interface we use in GuiSetup.
    // CardHand will be deprecated as this is supposed to replace it
    public Transform cardRoot;
    public Dictionary<Rank, Card> cards = new();

    // Spline/line layout config
    public AnimationCurve gapCurve; // maps proximity to hovered (1 near -> 0 far) to extra gap
    public float gapStrength = 1.5f; // scales extra gap beyond base spacing
    public float lineLength = 2.0f; // total length of the line centered at cardRoot
    public Card hoveredCard;
    List<Card> orderedCardsCache;

    // Layout configuration
    public float cardSpacing = 0.25f; // world units between card centers
    public Vector3 cardEulerOffset = Vector3.zero; // additional rotation for cards
    public float cardZOffsetPerIndex = 0.001f; // small depth offset to reduce z-fighting
    public bool arrangeAlongWidth = true; // true = along cardRoot.right, false = along cardRoot.up
    public bool autoLayoutEveryFrame = false; // set true if cardRoot can move/scale during setup
    public bool debugLayoutLogs = true; // verbose logging for debugging
    
    public void Initialize(GameNetworkState netState)
    {
        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.Initialize] Begin. cardRoot={(cardRoot != null ? cardRoot.name : "null")}" );
        }
        // clear old cards
        int destroyed = 0;
        foreach (Transform child in cardRoot) { Destroy(child.gameObject); destroyed++; }
        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.Initialize] Cleared {destroyed} existing children under cardRoot.");
        }
        cards = new();
        // populate cardRoot and cards
        uint[] maxRanks = netState.lobbyParameters.max_ranks;
        PawnDef[] pawnDefs = ResourceRoot.OrderedPawnDefs.ToArray();
        for (int i = 0; i < maxRanks.Length; i++)
        {
            PawnDef pawnDef = pawnDefs[i];
            if (pawnDef.rank == Rank.UNKNOWN)
            {
                continue;
            }
            if (debugLayoutLogs)
            {
                Debug.Log($"[SetupScreen.Initialize] Spawning card for rank {pawnDef.rank} (i={i}).");
            }
            GameObject cardPrefab;
            if (netState.userTeam == Team.RED)
            {
                cardPrefab = pawnDef.redCard;
            }
            else
            {
                cardPrefab = pawnDef.blueCard;
            }
            Card card = Instantiate(cardPrefab, cardRoot).GetComponent<Card>();
            card.SetBaseRotation(cardEulerOffset);
            // Subscribe to card hover events so SetupScreen can drive effects
            card.HoverEnter += OnCardHoverEnter;
            card.HoverExit += OnCardHoverExit;
            cards.Add(pawnDef.rank, card);
            if (debugLayoutLogs)
            {
                Transform t = card != null ? card.transform : null;
                Debug.Log($"[SetupScreen.Initialize] Spawned '{(t != null ? t.name : "null")}' localPos={(t != null ? t.localPosition.ToString("F3") : "null")} worldPos={(t != null ? t.position.ToString("F3") : "null")}.");
            }
        }

        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.Initialize] Total children under cardRoot after spawn: {cardRoot.childCount}");
        }
        BuildOrderedCache();
        ApplyLineLayout(null);
    }

    void Update()
    {
        if (autoLayoutEveryFrame)
        {
            ApplyLineLayout(hoveredCard);
        }
    }

    public void LayoutCards()
    {
        ApplyLineLayout(null);
    }

    void BuildOrderedCache()
    {
        if (cardRoot == null) return;
        if (orderedCardsCache == null) orderedCardsCache = new List<Card>(cardRoot.childCount);
        orderedCardsCache.Clear();
        for (int i = 0; i < cardRoot.childCount; i++)
        {
            Card c = cardRoot.GetChild(i).GetComponent<Card>();
            if (c != null) orderedCardsCache.Add(c);
        }
    }

    public void OnCardHoverEnter(Card card)
    {
        hoveredCard = card;
        ApplyLineLayout(card);
    }

    public void OnCardHoverExit(Card card)
    {
        if (hoveredCard == card)
        {
            hoveredCard = null;
        }
        ApplyLineLayout(null);
    }

    void ApplyLineLayout(Card target)
    {
        BuildOrderedCache();
        int n = orderedCardsCache != null ? orderedCardsCache.Count : 0;
        if (cardRoot == null || n == 0) return;

        Vector3 center = cardRoot.position;
        Vector3 axis = arrangeAlongWidth ? cardRoot.right : cardRoot.up;
        Vector3 forward = cardRoot.forward;
        Quaternion rotation = cardRoot.rotation * Quaternion.Euler(cardEulerOffset);

        // Baseline: n points evenly across fixed length [-L/2, +L/2]
        float baseGap = n > 1 ? (lineLength / (n - 1)) : 0f;
        float[] s0 = new float[n];
        for (int i = 0; i < n; i++) s0[i] = (-lineLength * 0.5f) + baseGap * i;

        int hoveredIndex = target != null ? orderedCardsCache.IndexOf(target) : -1;
        float[] s = new float[n];
        if (hoveredIndex < 0)
        {
            for (int i = 0; i < n; i++) s[i] = s0[i];
        }
        else
        {
            int gaps = n - 1;
            float[] w = new float[gaps];
            int leftCount = hoveredIndex; // number of gaps on left side
            int rightCount = (n - 1) - hoveredIndex; // number of gaps on right side

            // compute per-gap weights with proximity on each side
            for (int g = 0; g < gaps; g++)
            {
                bool isLeft = g < hoveredIndex;
                if (isLeft)
                {
                    int sideNorm = Mathf.Max(leftCount - 1, 1);
                    int dist = (hoveredIndex - 1) - g; // 0 near hovered
                    float t = leftCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(t) : t;
                    w[g] = 1f + gapStrength * curve;
                }
                else
                {
                    int sideNorm = Mathf.Max(rightCount - 1, 1);
                    int dist = g - hoveredIndex; // 0 near hovered
                    float t = rightCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(t) : t;
                    w[g] = 1f + gapStrength * curve;
                }
            }

            // normalize gaps separately per side so endpoints remain at ±L/2 and hovered stays anchored
            float leftSpan = baseGap * leftCount;
            float rightSpan = baseGap * rightCount;
            float sumLeft = 0f, sumRight = 0f;
            for (int g = 0; g < gaps; g++)
            {
                if (g < hoveredIndex) sumLeft += w[g]; else sumRight += w[g];
            }
            float[] gapLen = new float[gaps];
            for (int g = 0; g < gaps; g++)
            {
                if (g < hoveredIndex)
                {
                    gapLen[g] = leftCount > 0 ? (w[g] / (sumLeft > 0f ? sumLeft : leftCount)) * leftSpan : 0f;
                }
                else
                {
                    gapLen[g] = rightCount > 0 ? (w[g] / (sumRight > 0f ? sumRight : rightCount)) * rightSpan : 0f;
                }
            }

            // accumulate positions from hovered index outward
            s[hoveredIndex] = s0[hoveredIndex];
            for (int i = hoveredIndex + 1; i < n; i++)
            {
                s[i] = s[i - 1] + gapLen[i - 1];
            }
            for (int i = hoveredIndex - 1; i >= 0; i--)
            {
                s[i] = s[i + 1] - gapLen[i];
            }
        }

        for (int i = 0; i < n; i++)
        {
            Transform t = orderedCardsCache[i].transform;
            Vector3 pos = center + axis * s[i] + forward * (cardZOffsetPerIndex * i);
            t.position = pos;
            t.rotation = rotation;
        }
    }
}
