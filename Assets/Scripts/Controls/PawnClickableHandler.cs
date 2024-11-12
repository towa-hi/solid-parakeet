using UnityEngine;
using System;

public class PawnClickableHandler : MonoBehaviour
{
    public Clickable billboardClickable;
    public Clickable planeClickable;

    MeshRenderer billboardMesh;
    MeshRenderer planeMesh;

    public event Action OnHoverEnter;
    public event Action OnHoverExit;
    public event Action<Vector2> OnClick;

    int hoverCount = 0;
    bool isPointerOver;
    
    void Start()
    {
        // Subscribe to hover events
        billboardClickable.OnHoverEnter += HandleHoverEnter;
        billboardClickable.OnHoverExit += HandleHoverExit;
        planeClickable.OnHoverEnter += HandleHoverEnter;
        planeClickable.OnHoverExit += HandleHoverExit;

        // Subscribe to click events
        billboardClickable.OnClick += HandleClick;
        planeClickable.OnClick += HandleClick;

        // Get MeshRenderers
        billboardMesh = billboardClickable.GetComponent<MeshRenderer>();
        planeMesh = planeClickable.GetComponent<MeshRenderer>();

        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipeline != null)
        {
            var renderingLayerNames = pipeline.renderingLayerMaskNames;
            foreach (var name in renderingLayerNames)
            {
                Debug.Log(name);
            }
        }
        SetMeshOutline(isPointerOver);
    }

    void HandleHoverEnter()
    {
        hoverCount++;
        if (hoverCount == 1)
        {
            // Mouse has entered the pawn
            Debug.Log("Pawn Hover Enter");
            isPointerOver = true;
            OnHoverEnter?.Invoke();

            // Change layers to Wide Outline
            SetMeshOutline(true);
        }
    }

    void HandleHoverExit()
    {
        hoverCount--;
        if (hoverCount == 0)
        {
            // Mouse has exited all parts of the pawn
            Debug.Log("Pawn Hover Exit");
            isPointerOver = false;
            OnHoverExit?.Invoke();

            // Revert layers to original
            SetMeshOutline(false);
        }
    }

    void HandleClick(Vector2 pointerPosition)
    {
        Debug.Log("Pawn Clicked at: " + pointerPosition);
        OnClick?.Invoke(pointerPosition);
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (billboardClickable != null)
        {
            billboardClickable.OnHoverEnter -= HandleHoverEnter;
            billboardClickable.OnHoverExit -= HandleHoverExit;
            billboardClickable.OnClick -= HandleClick;
        }
        if (planeClickable != null)
        {
            planeClickable.OnHoverEnter -= HandleHoverEnter;
            planeClickable.OnHoverExit -= HandleHoverExit;
            planeClickable.OnClick -= HandleClick;
        }
    }

    void SetMeshOutline(bool enable)
    {
        if (billboardMesh != null && planeMesh != null)
        {
            if (enable)
            {
                billboardMesh.renderingLayerMask = (1 << 0) | (1 << 7);
                planeMesh.renderingLayerMask = (1 << 0) | (1 << 7);
            }
            else
            {
                billboardMesh.renderingLayerMask = (1 << 0);
                planeMesh.renderingLayerMask = (1 << 0);
            }
        }
    }
}
