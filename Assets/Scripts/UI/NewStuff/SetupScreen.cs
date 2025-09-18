using UnityEngine;
using UnityEngine.Splines;
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
    List<Transform> slots = new();

    // Spline/line layout config
    public AnimationCurve gapCurve; // maps proximity to hovered (1 near -> 0 far) to extra gap
    public float gapStrength = 1.5f; // scales extra gap beyond base spacing
    public Card hoveredCard;
    List<Card> orderedCardsCache;

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
    public bool arrangeAlongWidth = true; // true = along cardRoot.right, false = along cardRoot.up
    public bool autoLayoutEveryFrame = false; // set true if cardRoot can move/scale during setup
    public bool debugLayoutLogs = true; // verbose logging for debugging
    
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
            // Create slot under cardRoot
            GameObject slotGO = new GameObject($"CardSlot_{pawnDef.rank}");
            slotGO.transform.SetParent(cardRoot, false);
            slots.Add(slotGO.transform);
            // Spawn card under cardRoot
            Card card = Instantiate(cardPrefab, cardRoot).GetComponent<Card>();
            card.SetBaseRotation(cardEulerOffset);
            card.SetSlot(slotGO.transform);
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
        ApplySplineLayout(null);
    }

    void Update()
    {
        if (autoLayoutEveryFrame)
        {
            ApplySplineLayout(hoveredCard);
        }
    }

    public void LayoutCards()
    {
        ApplySplineLayout(null);
    }

    void BuildOrderedCache()
    {
        Transform parentTransform = cardRoot;
        if (parentTransform == null) return;
        if (orderedCardsCache == null) orderedCardsCache = new List<Card>(parentTransform.childCount);
        orderedCardsCache.Clear();
        foreach (var kv in cards) { if (kv.Value != null) orderedCardsCache.Add(kv.Value); }
    }

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

    void ApplySplineLayout(Card target)
    {
        BuildOrderedCache();
        int n = orderedCardsCache != null ? orderedCardsCache.Count : 0;
		if (debugLayoutLogs)
		{
			Debug.Log($"[SetupScreen.ApplySplineLayout] begin n={n} cardRoot={(cardRoot != null ? cardRoot.name : "null")} spline={(spline != null ? spline.name : "null")}");
		}
		if (cardRoot == null || n == 0 || spline == null)
		{
			if (debugLayoutLogs)
			{
				Debug.Log("[SetupScreen.ApplySplineLayout] early return: missing refs or no children");
			}
			return;
		}

        // Baseline: n parameters evenly across [0,1]
        float baseGapT = n > 1 ? (1f / (n - 1)) : 0f;
        float[] t0 = new float[n];
        for (int i = 0; i < n; i++) t0[i] = baseGapT * i;

		int hoveredIndex = target != null ? orderedCardsCache.IndexOf(target) : -1;
		if (debugLayoutLogs)
		{
			Debug.Log($"[SetupScreen.ApplySplineLayout] hoveredIndex={hoveredIndex}");
		}
        float[] ts = new float[n];
        if (hoveredIndex < 0)
        {
            for (int i = 0; i < n; i++) ts[i] = t0[i];
        }
        else
        {
            int gaps = n - 1;
            float[] w = new float[gaps];
            int leftCount = hoveredIndex;
            int rightCount = (n - 1) - hoveredIndex;
            for (int g = 0; g < gaps; g++)
            {
                bool isLeft = g < hoveredIndex;
                if (isLeft)
                {
                    int sideNorm = Mathf.Max(leftCount - 1, 1);
                    int dist = (hoveredIndex - 1) - g; // 0 near hovered
                    float prox = leftCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(prox) : prox;
                    w[g] = 1f + gapStrength * curve;
                }
                else
                {
                    int sideNorm = Mathf.Max(rightCount - 1, 1);
                    int dist = g - hoveredIndex; // 0 near hovered
                    float prox = rightCount <= 1 ? 1f : 1f - (float)dist / sideNorm;
                    float curve = gapCurve != null ? gapCurve.Evaluate(prox) : prox;
                    w[g] = 1f + gapStrength * curve;
                }
            }
            float leftSpanT = baseGapT * leftCount;
            float rightSpanT = baseGapT * rightCount;
            float sumLeft = 0f, sumRight = 0f;
            for (int g = 0; g < gaps; g++) { if (g < hoveredIndex) sumLeft += w[g]; else sumRight += w[g]; }
            float[] gapLenT = new float[gaps];
            for (int g = 0; g < gaps; g++)
            {
                if (g < hoveredIndex)
                    gapLenT[g] = leftCount > 0 ? (w[g] / (sumLeft > 0f ? sumLeft : leftCount)) * leftSpanT : 0f;
                else
                    gapLenT[g] = rightCount > 0 ? (w[g] / (sumRight > 0f ? sumRight : rightCount)) * rightSpanT : 0f;
            }
            ts[hoveredIndex] = t0[hoveredIndex];
            for (int i = hoveredIndex + 1; i < n; i++) ts[i] = ts[i - 1] + gapLenT[i - 1];
            for (int i = hoveredIndex - 1; i >= 0; i--) ts[i] = ts[i + 1] - gapLenT[i];
        }

		if (debugLayoutLogs)
		{
			try { Debug.Log($"[SetupScreen.ApplySplineLayout] t-array=[{string.Join(",", ts.Select(v => v.ToString("F3")))}]"); }
			catch { }
		}

        for (int i = 0; i < n; i++)
		{
            Transform slotTr = slots[i];
			float tt = Mathf.Clamp01(ts[i]);
			// SplineContainer.Evaluate* returns WORLD space; convert to LOCAL before assignment
			Vector3 worldPos = spline.EvaluatePosition(tt);
			Vector3 worldTangent = spline.EvaluateTangent(tt);
			Vector3 worldUp = spline.EvaluateUpVector(tt);
			Vector3 localPos = spline.transform.InverseTransformPoint(worldPos);
			Vector3 localTangent = spline.transform.InverseTransformDirection(worldTangent);
			Vector3 localUp = spline.transform.InverseTransformDirection(worldUp);
            Quaternion lrot;
            if (alignToSplineTangent)
            {
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
            Vector3 appliedOffset = forceYawZero ? new Vector3(cardEulerOffset.x, 0f, cardEulerOffset.z) : cardEulerOffset;

            // Fan-out roll around Z based on index distance from center
            float fanRoll = 0f;
            if (n > 1 && Mathf.Abs(fanRollMaxDegrees) > 0.0001f)
            {
                float center = (n - 1) * 0.5f;
                float norm = center > 0f ? (i - center) / center : 0f; // -1 .. +1
                float mag = Mathf.Abs(norm);
                float curve = fanRollCurve != null ? Mathf.Clamp01(fanRollCurve.Evaluate(mag)) : mag;
                fanRoll = Mathf.Sign(norm) * fanRollMaxDegrees * curve;
            }

            // Optional roll around tangent if using spline alignment
            float tangentRoll = (alignToSplineTangent && Mathf.Abs(rollAroundTangentDegrees) > 0.0001f) ? rollAroundTangentDegrees : 0f;
            Vector3 rollAxis = alignToSplineTangent ? Vector3.forward : (fanAxisLocal.sqrMagnitude > 0.0001f ? fanAxisLocal.normalized : Vector3.forward);
            Quaternion rollQ = Quaternion.AngleAxis(tangentRoll + fanRoll, rollAxis);
            slotTr.localRotation = lrot * rollQ * Quaternion.Euler(appliedOffset);
			if (debugLayoutLogs)
			{
                Debug.Log($"[SetupScreen.ApplySplineLayout] set SLOT i={i} t={tt:F3} worldPos={worldPos:F3} localPos={(localPos + zOffset):F3}");
			}
		}
    }
}
