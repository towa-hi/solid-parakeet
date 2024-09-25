using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Hoverable))]
public class TileFloorView : MonoBehaviour
{
    MeshRenderer meshRenderer;
    Hoverable hoverable;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        hoverable = GetComponent<Hoverable>();

        meshRenderer.enabled = false; // Hide mesh initially

        // Subscribe to hover events from Hoverable
        hoverable.OnHoverEnter += HandleHoverEnter;
        hoverable.OnHoverExit += HandleHoverExit;
    }

    void OnDestroy()
    {
        // Unsubscribe from hover events to prevent memory leaks
        hoverable.OnHoverEnter -= HandleHoverEnter;
        hoverable.OnHoverExit -= HandleHoverExit;
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
