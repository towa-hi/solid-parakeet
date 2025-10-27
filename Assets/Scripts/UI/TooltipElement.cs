using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TooltipElement : MonoBehaviour
{
    public string header;
    public string body;
    public LayerMask physicsMask = Physics.DefaultRaycastLayers;
    public float hoverDelay = 1f;

    GraphicRaycaster[] _graphicRaycasters;
    bool _isHovering;
    bool _shownForThisHover;
    float _hoverStartTime;

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
    }

    void Update()
    {
        Vector2 mousePosition = Globals.InputActions != null
            ? Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>()
            : (Vector2)Input.mousePosition;

        bool isOver = IsPointerOverThisUI(mousePosition) || IsPointerOverThisPhysics(mousePosition);

        if (isOver != _isHovering)
        {
            _isHovering = isOver;
            if (Tooltip.Instance != null)
            {
                if (_isHovering)
                {
                    // Start delay timer; do not show immediately
                    _hoverStartTime = Time.unscaledTime;
                    _shownForThisHover = false;
                    Tooltip.Instance.SetVisible(false);
                    Tooltip.Instance.SetLocked(false);
                }
                else
                {
                    Tooltip.Instance.SetVisible(false);
                    Tooltip.Instance.SetLocked(false);
                    _shownForThisHover = false;
                }
            }
        }

        if (_isHovering && !_shownForThisHover)
        {
            if (Time.unscaledTime - _hoverStartTime >= hoverDelay)
            {
                if (Tooltip.Instance != null)
                {
                    // Teleport and fade from zero when showing for a new target
                    Tooltip.Instance.ShowAtPointerResetFade(header, body);
                    Tooltip.Instance.SetLocked(true);
                    _shownForThisHover = true;
                }
            }
        }
    }

    bool IsPointerOverThisUI(Vector2 screenPos)
    {
        if (_graphicRaycasters == null || _graphicRaycasters.Length == 0) return false;
        if (EventSystem.current == null) return false;

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        for (int i = 0; i < _graphicRaycasters.Length; i++)
        {
            results.Clear();
            var raycaster = _graphicRaycasters[i];
            if (raycaster == null || !raycaster.isActiveAndEnabled) continue;
            raycaster.Raycast(ped, results);
            for (int r = 0; r < results.Count; r++)
            {
                GameObject go = results[r].gameObject;
                if (go == null) continue;
                if (go.transform == transform || go.transform.IsChildOf(transform))
                    return true;
            }
        }
        return false;
    }

    bool IsPointerOverThisPhysics(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Camera.current;
        if (cam == null)
        {
            if (Camera.allCamerasCount > 0)
            {
                cam = Camera.allCameras[0];
            }
        }
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, physicsMask, QueryTriggerInteraction.Collide))
        {
            // Consider any collider within the same TileView hierarchy as a hover over this element
            Transform myRoot = GetComponentInParent<TileView>() ? GetComponentInParent<TileView>().transform : transform;
            if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                return true;
            if (myRoot != null && (hit.transform == myRoot || hit.transform.IsChildOf(myRoot)))
                return true;
            TileView hitTile = hit.transform.GetComponentInParent<TileView>();
            TileView myTile = GetComponentInParent<TileView>();
            if (hitTile != null && myTile != null && hitTile == myTile)
                return true;
        }
        return false;
    }
}
