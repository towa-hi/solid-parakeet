using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Clickable))]
public class TileFloorView : MonoBehaviour
{
    MeshRenderer meshRenderer;
    Clickable clickable;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        clickable = GetComponent<Clickable>();

        meshRenderer.enabled = false; // Hide mesh initially

        // Subscribe to hover events from Hoverable
        clickable.OnHoverEnter += HandleHoverEnter;
        clickable.OnHoverExit += HandleHoverExit;
    }

    void OnDestroy()
    {
        // Unsubscribe from hover events to prevent memory leaks
        clickable.OnHoverEnter -= HandleHoverEnter;
        clickable.OnHoverExit -= HandleHoverExit;
    }

    void HandleHoverEnter()
    {
        // Show the mesh when the pointer enters
        meshRenderer.enabled = true;
    }

    void HandleHoverExit()
    {
        // Hide the mesh when the pointer exits
        meshRenderer.enabled = false;
    }
}
