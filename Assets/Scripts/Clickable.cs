using UnityEngine;
using System;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Clickable : MonoBehaviour
{
    // Events for hover and click interactions
    public event Action OnHoverEnter;
    public event Action OnHoverExit;
    public event Action OnClick;

    [SerializeField] bool isPointerOver = false;

    // Optional: Layer mask to optimize raycasting
    public LayerMask interactionLayerMask = ~0; // Default to all layers

    void Awake()
    {
        // Subscribe to the Click action
        Globals.inputActions.Game.Click.performed += OnClickPerformed;
    }

    void OnDestroy()
    {
        // Unsubscribe from the Click action
        Globals.inputActions.Game.Click.performed -= OnClickPerformed;
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

    void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (IsPointerOver())
        {
            OnClick?.Invoke();
        }
    }

    bool IsPointerOver()
    {
        // Read the pointer position from the input actions
        Vector2 pointerPosition = Globals.inputActions.Game.PointerPosition.ReadValue<Vector2>();

        // Cast a ray from the camera through the pointer position
        Ray ray = GameManager.instance.mainCamera.ScreenPointToRay(pointerPosition);

        // Perform a RaycastAll to get all hits along the ray, using the layer mask
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, interactionLayerMask);

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
