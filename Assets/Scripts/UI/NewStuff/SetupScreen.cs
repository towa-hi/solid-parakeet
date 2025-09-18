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
        LayoutCards();
    }

    void Update()
    {
        if (autoLayoutEveryFrame)
        {
            LayoutCards();
        }
    }

    public void LayoutCards()
    {
        if (cardRoot == null)
        {
            if (debugLayoutLogs)
            {
                Debug.Log("[SetupScreen.LayoutCards] Early return: cardRoot is null");
            }
            return;
        }

        int childCount = cardRoot.childCount;
        if (childCount == 0)
        {
            if (debugLayoutLogs)
            {
                Debug.Log("[SetupScreen.LayoutCards] Early return: no children to layout");
            }
            return;
        }

        Vector3 center = cardRoot.position;
        Vector3 axis = arrangeAlongWidth ? cardRoot.right : cardRoot.up;
        Quaternion rotation = cardRoot.rotation * Quaternion.Euler(cardEulerOffset);
        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.LayoutCards] childCount={childCount} center={center:F3} axis={axis:F3} arrangeAlongWidth={arrangeAlongWidth} spacing={cardSpacing} zPerIndex={cardZOffsetPerIndex} eulerOffset={cardEulerOffset}");
        }

        if (childCount == 1)
        {
            Transform only = cardRoot.GetChild(0);
            if (debugLayoutLogs)
            {
                Debug.Log($"[SetupScreen.LayoutCards] Single child '{only.name}' before worldPos={only.position:F3} localPos={only.localPosition:F3}");
            }
            only.position = center;
            only.rotation = rotation;
            if (debugLayoutLogs)
            {
                Debug.Log($"[SetupScreen.LayoutCards] Single child '{only.name}' after worldPos={only.position:F3} localPos={only.localPosition:F3}");
            }
            return;
        }

        // Symmetric spacing around center: indexOffset in [-k, +k]
        float mid = (childCount - 1) * 0.5f;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = cardRoot.GetChild(i);
            Vector3 beforeWorld = child.position;
            Vector3 beforeLocal = child.localPosition;
            float offsetIndex = i - mid; // e.g., for 5: -2,-1,0,1,2
            Vector3 pos = center + axis * (offsetIndex * cardSpacing);
            Vector3 depthOffset = cardRoot.forward * (cardZOffsetPerIndex * i);
            child.position = pos + depthOffset;
            child.rotation = rotation;
            if (debugLayoutLogs)
            {
                Debug.Log($"[SetupScreen.LayoutCards] i={i} '{child.name}' offsetIndex={offsetIndex} beforeW={beforeWorld:F3} beforeL={beforeLocal:F3} afterW={child.position:F3} afterL={child.localPosition:F3}");
            }
        }
    }
}
