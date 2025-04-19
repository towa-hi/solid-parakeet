using System;
using System.Collections.Generic;
using UnityEngine;

public class RenderEffect : MonoBehaviour
{
    public List<Renderer> renderers;
    uint currentRenderingLayerMask;
    
    public void SetEffect(EffectType effectType, bool enable)
    {
        uint layer = effectType switch
        {
            EffectType.FILL => (1u << 6),
            EffectType.HOVEROUTLINE => (1u << 7),
            EffectType.SELECTOUTLINE => (1u << 8),
            _ => throw new ArgumentException(),
        };
        if (enable)
        {
            currentRenderingLayerMask |= layer;
        }
        else
        {
            currentRenderingLayerMask &= ~layer;
        }
        currentRenderingLayerMask |= (1u << 0);
        foreach (Renderer rend in renderers)
        {
            rend.renderingLayerMask = currentRenderingLayerMask;
        }
    }

    public void ClearEffects()
    {
        foreach (EffectType effect in Enum.GetValues(typeof(EffectType)))
        {
            SetEffect(effect, false);
        }
    }
}

public enum EffectType
{
    FILL,
    HOVEROUTLINE,
    SELECTOUTLINE,
}