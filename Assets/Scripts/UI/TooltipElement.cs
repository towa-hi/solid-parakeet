using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface ITooltipContentProvider
{
    bool TryGetTooltipContent(TooltipElement element, out string header, out string body);
}

public class TooltipElement : MonoBehaviour
{
    public string header;
    public string body;
    public float hoverDelay = 1f;
    
    GraphicRaycaster[] _graphicRaycasters;
    ITooltipContentProvider _contentProvider;
    bool _hasTilePosition;
    Vector2Int _tilePosition;
    
    void Awake()
    {
        _graphicRaycasters = GetComponentsInParent<GraphicRaycaster>(true);
        ResolveContentProvider();
    }

    void OnEnable()
    {
        ResolveContentProvider();
    }

    void ResolveContentProvider()
    {
        _contentProvider = GetComponent<ITooltipContentProvider>();

        if (_contentProvider == null)
        {
            _contentProvider = GetComponentInParent<ITooltipContentProvider>();
        }
    }

    public bool TryGetTooltipContent(out string tooltipHeader, out string tooltipBody)
    {
        if (_contentProvider != null)
        {
            return _contentProvider.TryGetTooltipContent(this, out tooltipHeader, out tooltipBody);
        }

        tooltipHeader = header;
        tooltipBody = body;
        return !string.IsNullOrEmpty(tooltipHeader) || !string.IsNullOrEmpty(tooltipBody);
    }

    public void SetTilePosition(Vector2Int pos)
    {
        _tilePosition = pos;
        _hasTilePosition = true;
    }

    public bool TryGetTilePosition(out Vector2Int pos)
    {
        pos = _tilePosition;
        return _hasTilePosition;
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
