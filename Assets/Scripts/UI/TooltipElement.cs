using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TooltipElement : MonoBehaviour
{
    public string header;
    public string body;
    public float hoverDelay = 1f;
    
    GraphicRaycaster[] _graphicRaycasters;
    
    void Awake()
    {
        _graphicRaycasters = GetComponentsInParent<GraphicRaycaster>(true);
    }
    public void SetTooltipEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
    public void SetTooltipText(string header, string body)
    {
        this.header = header;
        this.body = body;
        // If this element is currently active, update instantly without resetting timers/fade
        if (Tooltip.Instance != null && Tooltip.Instance.CurrentElement == this)
        {
            Tooltip.Instance.SetText(this.header, this.body);
        }
    }
    // Content-only; hover detection is centralized in Tooltip
}
