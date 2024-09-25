using UnityEngine;
using System;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Hoverable : MonoBehaviour
{
    public event Action OnHoverEnter;
    public event Action OnHoverExit;

    bool isPointerOver = false;

    // Static instance of GameInputActions to be shared among all Hoverable instances
    static InputSystem_Actions inputActions;

    // Optional: Layer mask to optimize raycasting
    public LayerMask hoverLayerMask = ~0; // Default to all layers

    void Awake()
    {
        // Initialize the input actions if not already done
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            inputActions.Enable();
        }
    }

    void OnDestroy()
    {
        // Optionally disable input actions if needed
        // Be cautious with static instances
    }

    void Update()
    {
        bool pointerOverNow = IsPointerOver();

        if (pointerOverNow && !isPointerOver)
        {
            isPointerOver = true;
            OnHoverEnter?.Invoke();
        }
        else if (!pointerOverNow && isPointerOver)
        {
            isPointerOver = false;
            OnHoverExit?.Invoke();
        }
    }

    bool IsPointerOver()
    {
        // Read the pointer position from the input actions
        Vector2 pointerPosition = inputActions.Game.PointerPosition.ReadValue<Vector2>();

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
