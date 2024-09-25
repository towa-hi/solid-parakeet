using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Hoverable : MonoBehaviour
{
    public event Action OnHoverEnter;
    public event Action OnHoverExit;

    private bool isPointerOver = false;
    private InputAction pointerPositionAction;

    // Optional: Layer mask to optimize raycasting
    public LayerMask hoverLayerMask = ~0; // Default to all layers

    void Awake()
    {
        // Create a new InputAction for pointer position
        pointerPositionAction = new InputAction(type: InputActionType.Value, binding: "<Pointer>/position");
        pointerPositionAction.Enable();
    }

    void OnDestroy()
    {
        pointerPositionAction.Disable();
    }

    void Update()
    {
        bool pointerOverNow = IsPointerOver();
        if (pointerOverNow && !isPointerOver)
        {
            // Pointer has just entered
            isPointerOver = true;
            OnHoverEnter?.Invoke();
        }
        else if (!pointerOverNow && isPointerOver)
        {
            // Pointer has just exited
            isPointerOver = false;
            OnHoverExit?.Invoke();
        }
    }

    private bool IsPointerOver()
    {
        // Read the pointer position from the InputAction
        Vector2 pointerPosition = pointerPositionAction.ReadValue<Vector2>();

        // Cast a ray from the camera through the pointer position
        Ray ray = GameManager.instance.mainCamera.ScreenPointToRay(pointerPosition);

        // Perform a RaycastAll to get all hits along the ray, using the layer mask
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, hoverLayerMask);

        // Check if this GameObject is among the hits
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject)
            {
                return true;
            }
        }
        return false;
    }
}