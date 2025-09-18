using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CardSorting sorting;

    public Transform pivot;
    public CardRotation rotation;

    public RenderEffect renderEffect;

    public Vector3 baseRotationEuler;

    public void SetBaseRotation(Vector3 rotationEuler)
    {
        baseRotationEuler = rotationEuler;
        pivot.localEulerAngles = baseRotationEuler;
    }

    void OnMouseEnter()
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, true);
        }
    }

    void OnMouseExit()
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.CARDHOVEROUTLINE, false);
        }
    }

    void OnDisable()
    {
        if (renderEffect != null)
        {
            renderEffect.SetEffect(EffectType.HOVEROUTLINE, false);
        }
    }
}
