using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
 

[DefaultExecutionOrder(10000)]
public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CardSorting sorting;

    public Transform pivot;
    public CardRotation rotation;

    public RenderEffect renderEffect;

    public Vector3 baseRotationEuler;

		// Simple slot following
		[Header("Follow Slot Settings")]
		[Tooltip("The slot transform this card should follow.")]
		public Transform slot;
		[Tooltip("How quickly the card eases toward its slot position.")]
		public float followPositionLerpSpeed = 12f;
		[Tooltip("How quickly the card eases toward its slot rotation.")]
		public float followRotationLerpSpeed = 12f;

    public event System.Action<Card> HoverEnter;
    public event System.Action<Card> HoverExit;
    bool isHovered;

    // Pivot local-position tween state
    Vector3 basePivotLocalPosition;
    public Vector3 currentLocalOffset;
    Coroutine localOffsetLerpRoutine;
    Vector3 currentTargetLocalOffset;


    public void SetRoot(Transform root) { /* no-op in slot-follow model */ }

    public void SetSlot(Transform t)
    {
        slot = t;
    }

    public void SetBaseRotation(Vector3 rotationEuler)
    {
        baseRotationEuler = rotationEuler;
        pivot.localEulerAngles = baseRotationEuler;
    }

    void Awake()
    {
        if (pivot != null)
        {
            basePivotLocalPosition = pivot.localPosition;
        }
			isHovered = false;
			if (pivot != null)
			{
				pivot.localPosition = basePivotLocalPosition;
			}
    }

    // Interruptible lerp of pivot localPosition to base + targetLocalOffset
		public void LocalOffsetLerp(Vector3 targetLocalOffset, float duration, AnimationCurve easing = null)
    {
        if (pivot == null)
        {
            return;
        }
        // Early-out if already targeting approximately the same offset
        if (localOffsetLerpRoutine != null && Approximately(targetLocalOffset, currentTargetLocalOffset))
        {
            return;
        }
        if (localOffsetLerpRoutine != null)
        {
            StopCoroutine(localOffsetLerpRoutine);
            localOffsetLerpRoutine = null;
        }
        if (duration <= 0f)
        {
            currentLocalOffset = targetLocalOffset;
            currentTargetLocalOffset = targetLocalOffset;
            return;
        }
        currentTargetLocalOffset = targetLocalOffset;
        localOffsetLerpRoutine = StartCoroutine(LocalOffsetLerpRoutine(targetLocalOffset, duration, easing));
    }

		IEnumerator LocalOffsetLerpRoutine(Vector3 targetLocalOffset, float duration, AnimationCurve easing)
    {
        Vector3 startOffset = currentLocalOffset;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            float e = easing != null ? easing.Evaluate(u) : EaseInOutCubic(u);
            currentLocalOffset = Vector3.LerpUnclamped(startOffset, targetLocalOffset, e);
				if (pivot != null) pivot.localPosition = basePivotLocalPosition + currentLocalOffset;
				yield return null;
        }
        currentLocalOffset = targetLocalOffset;
			if (pivot != null) pivot.localPosition = basePivotLocalPosition + currentLocalOffset;
			localOffsetLerpRoutine = null;
    }

		void LateUpdate()
		{
			if (pivot == null)
			{
				return;
			}
			// Keep pivot aligned to current local offset
			pivot.localPosition = basePivotLocalPosition + currentLocalOffset;

			// Follow assigned slot with easing
			if (slot != null)
			{
				float sp = 1f - Mathf.Exp(-followPositionLerpSpeed * Time.deltaTime);
				float sr = 1f - Mathf.Exp(-followRotationLerpSpeed * Time.deltaTime);
				transform.position = Vector3.Lerp(transform.position, slot.position, sp);
				transform.rotation = Quaternion.Slerp(transform.rotation, slot.rotation, sr);
			}
		}

		// Removed constraint-based code for simplicity; card follows assigned slot.

    static float EaseInOutCubic(float x)
    {
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    static bool Approximately(in Vector3 a, in Vector3 b)
    {
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.z, b.z);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, true);
        }
        if (!isHovered)
        {
            isHovered = true;
            HoverEnter?.Invoke(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, false);
        }
        if (isHovered)
        {
            isHovered = false;
            HoverExit?.Invoke(this);
        }
    }

    void OnDisable()
    {
        if (localOffsetLerpRoutine != null)
        {
            StopCoroutine(localOffsetLerpRoutine);
            localOffsetLerpRoutine = null;
        }
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, false);
        }
        if (isHovered)
        {
            isHovered = false;
            HoverExit?.Invoke(this);
        }
    }
}
