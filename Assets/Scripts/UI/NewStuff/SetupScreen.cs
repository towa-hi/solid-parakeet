using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

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
    public Transform origin;

    public Dictionary<Rank, Card> cards = new();
    List<Transform> slots = new();
    public Card selectedCard;

    // Spline/line layout config
    public AnimationCurve gapCurve; // maps proximity to hovered (1 near -> 0 far) to extra gap
    public float gapStrength = 1.5f; // scales extra gap beyond base spacing
    public Card hoveredCard;
    // Cache removed: we resolve indices by slot order directly

    public SplineContainer spline;
    public bool alignToSplineTangent = false; // if true, cards face along spline; else keep yaw 0
    public bool forceYawZero = true; // force Y rotation to 0 regardless of tangent
    public float rollAroundTangentDegrees = 0f; // additional roll around spline tangent (Z in tangent frame)
    public float fanRollMaxDegrees = 15f; // max Z-roll for outermost cards to fan like a hand
    public AnimationCurve fanRollCurve; // maps 0..1 (center->edge) to 0..1 roll multiplier
    public Vector3 fanAxisLocal = Vector3.forward; // axis to fan around when not aligning to tangent

    // Layout configuration
    public Vector3 cardEulerOffset = Vector3.zero; // additional rotation for cards
    public float cardZOffsetPerIndex = 0.001f; // small depth offset to reduce z-fighting
    // Removed: arrangeAlongWidth (unused orientation option)
    public bool autoLayoutEveryFrame = false; // set true if cardRoot can move/scale during setup
    public bool debugLayoutLogs = false; // verbose logging for debugging
    public System.Action<Rank> OnCardRankClicked;
    [Header("Initial Slot Placement")]
    public bool animateSlotsOnInitialize = true;
    public float slotAddInterval = 0.06f;
    bool initialized;
    bool pendingOpenAnimation;
    [Header("Hover Presentation")]
    public float hoverYOffsetLocal = 0.05f;
    
    void OnEnable()
    {
        if (cardRoot) cardRoot.gameObject.SetActive(true);
    }
    void OnDisable()
    {
        if (cardRoot) cardRoot.gameObject.SetActive(false);
    }
    public void Initialize(GameNetworkState netState)
    {
        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.Initialize] Begin. cardRoot={(cardRoot != null ? cardRoot.name : "null")}" );
        }
        // clear old cards and slots
        int destroyed = 0;
        foreach (Transform child in cardRoot) { Destroy(child.gameObject); destroyed++; }
        if (debugLayoutLogs)
        {
            Debug.Log($"[SetupScreen.Initialize] Cleared {destroyed} existing children under cardRoot.");
        }
        cards = new();
        slots = new();
        // populate cardRoot and cards
        uint[] maxRanks = netState.lobbyParameters.max_ranks;
        PawnDef[] pawnDefs = ResourceRoot.OrderedPawnDefs.ToArray();
        for (int i = 0; i < maxRanks.Length; i++)
        {
            PawnDef pawnDef = pawnDefs[i];
			// // Only spawn cards for ranks with a positive max in max_ranks
			// if (maxRanks[i] == 0)
			// {
			// 	if (debugLayoutLogs)
			// 	{
			// 		Debug.Log($"[SetupScreen.Initialize] Skipping rank {pawnDef.rank} (i={i}) because max_ranks is 0.");
			// 	}
			// 	continue;
			// }
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
            // Create slot at origin, then parent under cardRoot preserving world pose
            GameObject slotGO = new GameObject($"CardSlot_{pawnDef.rank}");
            Vector3 spawnPos = origin != null ? origin.position : Vector3.zero;
            Quaternion spawnRot = origin != null ? origin.rotation : Quaternion.identity;
            slotGO.transform.position = spawnPos;
            slotGO.transform.rotation = spawnRot;
            slotGO.transform.SetParent(cardRoot, true);
            slots.Add(slotGO.transform);
            // Spawn card under cardRoot
            // Spawn card at origin, then parent under cardRoot preserving world pose
            Card card = Instantiate(cardPrefab).GetComponent<Card>();
            Transform ct = card.transform;
            ct.position = spawnPos;
            ct.rotation = spawnRot;
            ct.SetParent(cardRoot, true);
            card.SetBaseRotation(cardEulerOffset);
            card.SetSlot(slotGO.transform);
            // Subscribe to card hover events so SetupScreen can drive effects
            card.HoverEnter += OnCardHoverEnter;
            card.HoverExit += OnCardHoverExit;
            card.Clicked += OnCardClicked;
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
        // Always apply a static initial layout; defer animation until panel is active
        ApplySplineLayout(null);
        initialized = true;
        pendingOpenAnimation = animateSlotsOnInitialize;
    }

    public void Uninitialize()
    {
        if (debugLayoutLogs)
        {
            Debug.Log("[SetupScreen.Uninitialize] Begin.");
        }
        // Stop any running coroutines started by this component (e.g., AnimateInitialLayout)
        StopAllCoroutines();
        initialized = false;
        pendingOpenAnimation = false;

        // Clear selection/hover visuals
        if (selectedCard != null)
        {
            if (selectedCard.renderEffect != null)
            {
                selectedCard.renderEffect.SetEffect(EffectType.SELECTOUTLINE, false);
            }
            selectedCard.SetSelected(false);
            selectedCard = null;
        }
        hoveredCard = null;

        // Unsubscribe from card events
        if (cards != null)
        {
            foreach (var kv in cards)
            {
                Card c = kv.Value;
                if (c == null) continue;
                c.HoverEnter -= OnCardHoverEnter;
                c.HoverExit -= OnCardHoverExit;
                c.Clicked -= OnCardClicked;
            }
            cards.Clear();
        }

        // Destroy all spawned children (cards and slots)
        if (cardRoot != null)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in cardRoot)
            {
                children.Add(child.gameObject);
            }
            int destroyed = 0;
            for (int i = 0; i < children.Count; i++)
            {
                Destroy(children[i]);
                destroyed++;
            }
            if (debugLayoutLogs)
            {
                Debug.Log($"[SetupScreen.Uninitialize] Destroyed {destroyed} children under cardRoot.");
            }
        }

        // Clear slot references
        if (slots != null)
        {
            slots.Clear();
        }

        // Remove external callbacks
        OnCardRankClicked = null;

        if (debugLayoutLogs)
        {
            Debug.Log("[SetupScreen.Uninitialize] End.");
        }
    }

    void Update()
    {
        if (autoLayoutEveryFrame)
        {
            ApplySplineLayout(hoveredCard);
        }
    }

    // Removed: LayoutCards (unused entry point)

    // Removed: BuildOrderedCache (no longer needed)

    public void OnCardHoverEnter(Card card)
    {
        hoveredCard = card;
        ApplySplineLayout(card);
    }

    public void OnCardHoverExit(Card card)
    {
        if (hoveredCard == card)
        {
            hoveredCard = null;
        }
        ApplySplineLayout(null);
    }

    void OnCardClicked(Card card)
    {
        // Toggle selection outline on clicked card; clear previous
        if (selectedCard != null && selectedCard.renderEffect != null)
        {
            selectedCard.renderEffect.SetEffect(EffectType.SELECTOUTLINE, false);
            selectedCard.SetSelected(false);
        }
        if (selectedCard == card)
        {
            selectedCard = null;
        }
        else
        {
            selectedCard = card;
            if (selectedCard.renderEffect != null)
            {
                selectedCard.renderEffect.SetEffect(EffectType.SELECTOUTLINE, true);
            }
            selectedCard.SetSelected(true);
        }
        // Notify GuiSetup to behave like the list UI
        foreach (var kv in cards)
        {
            if (kv.Value == card)
            {
                OnCardRankClicked?.Invoke(kv.Key);
                break;
            }
        }
        // Re-apply layout: prefer hovered if present, else selected
        ApplySplineLayout(hoveredCard);
    }

    void ApplySplineLayout(Card target)
    {
        int n = slots != null ? slots.Count : 0;
        ApplySplineLayoutPartial(target, n);
    }

    System.Collections.IEnumerator AnimateInitialLayout()
    {
        // Lay out slots one by one at intervals
        int n = slots != null ? slots.Count : 0;
        for (int i = 0; i < n; i++)
        {
            ApplySplineLayoutPartial(hoveredCard, i + 1);
            if (slotAddInterval > 0f)
            {
                yield return new WaitForSeconds(slotAddInterval);
            }
            else
            {
                yield return null;
            }
        }
    }

    void ApplySplineLayoutPartial(Card target, int count)
    {
        int nTotal = slots != null ? slots.Count : 0;
        if (cardRoot == null || nTotal == 0 || spline == null) return;
        int n = Mathf.Clamp(count, 0, nTotal);
        if (n == 0) return;

        // Baseline spacing for first n only
        float baseGapT = n > 1 ? (1f / (n - 1)) : 0f;

        // Hover index used ONLY for vertical offset presentation
        int hoverIndex = -1;
        if (target != null && target.slot != null)
        {
            hoverIndex = slots.IndexOf(target.slot);
        }
        if (hoverIndex < 0 || hoverIndex >= n)
        {
            hoverIndex = -1;
        }

        // Layout anchor (hover preferred, else selection) used for spacing
        Card effectiveTarget = target != null ? target : selectedCard;
        int layoutIndex = -1;
        if (effectiveTarget != null && effectiveTarget.slot != null)
        {
            layoutIndex = slots.IndexOf(effectiveTarget.slot);
        }
        if (layoutIndex < 0 || layoutIndex >= n)
        {
            layoutIndex = -1;
        }

        float[] ts = new float[n];
        if (layoutIndex < 0)
        {
            for (int i = 0; i < n; i++) ts[i] = baseGapT * i;
        }
        else
        {
            int gaps = n - 1;
            float[] w = new float[gaps];
            int leftCount = layoutIndex;
            int rightCount = (n - 1) - layoutIndex;
            for (int g = 0; g < gaps; g++)
            {
                bool isLeft = g < layoutIndex;
                if (isLeft)
                {
                    int sideNorm = Mathf.Max(leftCount - 1, 1);
                    int dist = (layoutIndex - 1) - g;
                    float prox = leftCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(prox) : prox;
                    w[g] = 1f + gapStrength * curve;
                }
                else
                {
                    int sideNorm = Mathf.Max(rightCount - 1, 1);
                    int dist = g - layoutIndex;
                    float prox = rightCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(prox) : prox;
                    w[g] = 1f + gapStrength * curve;
                }
            }
            float leftSpanT = baseGapT * leftCount;
            float rightSpanT = baseGapT * rightCount;
            float sumLeft = 0f, sumRight = 0f;
            for (int g = 0; g < gaps; g++) { if (g < layoutIndex) sumLeft += w[g]; else sumRight += w[g]; }
            float[] gapLenT = new float[gaps];
            for (int g = 0; g < gaps; g++)
            {
                if (g < layoutIndex)
                    gapLenT[g] = leftCount > 0 ? (w[g] / (sumLeft > 0f ? sumLeft : leftCount)) * leftSpanT : 0f;
                else
                    gapLenT[g] = rightCount > 0 ? (w[g] / (sumRight > 0f ? sumRight : rightCount)) * rightSpanT : 0f;
            }
            ts[layoutIndex] = baseGapT * layoutIndex;
            for (int i = layoutIndex + 1; i < n; i++) ts[i] = ts[i - 1] + gapLenT[i - 1];
            for (int i = layoutIndex - 1; i >= 0; i--) ts[i] = ts[i + 1] - gapLenT[i];
        }

        // Precompute constants used in the loop
        Vector3 appliedOffset = forceYawZero ? new Vector3(cardEulerOffset.x, 0f, cardEulerOffset.z) : cardEulerOffset;
        float tangentRoll = (alignToSplineTangent && Mathf.Abs(rollAroundTangentDegrees) > 0.0001f) ? rollAroundTangentDegrees : 0f;
        Vector3 rollAxis = alignToSplineTangent ? Vector3.forward : (fanAxisLocal.sqrMagnitude > 0.0001f ? fanAxisLocal.normalized : Vector3.forward);

        for (int i = 0; i < n; i++)
        {
            Transform slotTr = slots[i];
            float tt = Mathf.Clamp01(ts[i]);
            Vector3 worldPos = spline.EvaluatePosition(tt);
            Vector3 localPos = spline.transform.InverseTransformPoint(worldPos);
            if (hoverIndex >= 0 && hoverYOffsetLocal != 0f)
            {
                localPos.y += hoverYOffsetLocal;
            }
            Quaternion lrot;
            if (alignToSplineTangent)
            {
                Vector3 worldTangent = spline.EvaluateTangent(tt);
                Vector3 worldUp = spline.EvaluateUpVector(tt);
                Vector3 localTangent = spline.transform.InverseTransformDirection(worldTangent);
                Vector3 localUp = spline.transform.InverseTransformDirection(worldUp);
                Vector3 lt = localTangent.sqrMagnitude > 0.0001f ? localTangent.normalized : Vector3.forward;
                Vector3 lu = localUp.sqrMagnitude > 0.0001f ? localUp.normalized : Vector3.up;
                lrot = Quaternion.LookRotation(lt, lu);
            }
            else
            {
                lrot = Quaternion.identity;
            }
            Vector3 zOffset = Vector3.forward * (cardZOffsetPerIndex * i);
            slotTr.localPosition = localPos + zOffset;
            float fanRoll = 0f;
            if (n > 1 && Mathf.Abs(fanRollMaxDegrees) > 0.0001f)
            {
                float center = (n - 1) * 0.5f;
                float norm = center > 0f ? (i - center) / center : 0f;
                float mag = Mathf.Abs(norm);
                float curve = fanRollCurve != null ? Mathf.Clamp01(fanRollCurve.Evaluate(mag)) : mag;
                fanRoll = Mathf.Sign(norm) * fanRollMaxDegrees * curve;
            }
            Quaternion rollQ = Quaternion.AngleAxis(tangentRoll + fanRoll, rollAxis);
            slotTr.localRotation = lrot * rollQ * Quaternion.Euler(appliedOffset);
        }
    }

    public void PlayOpenAnimation()
    {
        if (!initialized) return;
        if (!gameObject.activeInHierarchy || !isActiveAndEnabled) return;
        if (!pendingOpenAnimation) return;
        StopAllCoroutines();
        pendingOpenAnimation = false;
        StartCoroutine(AnimateInitialLayout());
    }

    public void RefreshFromRanks((Rank rank, int max, int committed)[] ranksRemaining)
    {
        if (ranksRemaining == null || cards == null) return;
        for (int i = 0; i < ranksRemaining.Length; i++)
        {
            var (rank, max, committed) = ranksRemaining[i];
            if (cards.TryGetValue(rank, out Card card) && card != null && card.renderEffect != null)
            {
                bool noneLeft = committed >= max;
                card.renderEffect.SetEffect(EffectType.FILL, noneLeft);
            }
        }
    }
}
