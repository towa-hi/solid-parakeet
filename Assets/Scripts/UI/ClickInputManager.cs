using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickInputManager : MonoBehaviour
{
    public Vector2Int hoveredPosition; // grid position that the pointer is hovered over after hit check
    public Vector2 screenPointerPosition; // raw position on screen
    public bool isInitialized;

    public GameObject hoveredObject; // first object closest to the camera that the pointer is over
    bool isUI; // is pointer over UI element 
    public PawnView hoveredPawnView; // closest pawnView pointer is over
    public TileView hoveredTileView; // closest tileView pointer is over

    Dictionary<int, int> layerPriorities;

    public event Action<Vector2Int, Vector2Int> OnPositionHovered;
    public event Action<Vector2, Vector2Int> OnClick;
    
    void Awake()
    {
        layerPriorities = new()
        {
            { LayerMask.NameToLayer("UI"), 0 }, // UI
            { LayerMask.NameToLayer("PawnView"), 1 }, // Highest priority
            { LayerMask.NameToLayer("TileView"), 2 }, // Lower priority
            { LayerMask.NameToLayer("Default"), 3 }, // Default priority for other layers
        };
    }

    public void Initialize()
    {
        isInitialized = true;
        hoveredPosition = Globals.PURGATORY;
        hoveredObject = null;
        hoveredPawnView = null;
        hoveredTileView = null;
    }

    public void Reset()
    {
        hoveredPawnView = null;
        hoveredTileView = null;
        Vector2Int oldHoveredPosition = hoveredPosition;
        hoveredPosition = Globals.PURGATORY;
        OnPositionHovered?.Invoke(oldHoveredPosition, hoveredPosition);
    }
    
    void Update()
    {
        if (!isInitialized) return;
        // get screen pointer position
        screenPointerPosition = Globals.inputActions.Game.PointerPosition.ReadValue<Vector2>();
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPointerPosition
        };
        // get list of hits
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        // sort so UI is always on top, then PawnViews then TileViews
        results.Sort((a, b) =>
        {
            int layerA = a.gameObject.layer;
            int layerB = b.gameObject.layer;
            int priorityA = layerPriorities.TryGetValue(layerA, out int priority) ? priority : int.MaxValue;
            int priorityB = layerPriorities.TryGetValue(layerB, out int layerPriority) ? layerPriority : int.MaxValue;
            return priorityA.CompareTo(priorityB);
        });
        bool hitUI = false;
        bool hitPawnView = false;
        bool hitTileView = false;
        GameObject currentHoveredObject = results.Count == 0 ? null : results[0].gameObject;
        PawnView currentHoveredPawnView = null;
        TileView currentHoveredTileView = null;
        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            int hitLayer = hitObject.layer;
            if (!hitUI && hitLayer == LayerMask.NameToLayer("UI"))
            {
                hitUI = true;
            }
            if (!hitPawnView && hitLayer == LayerMask.NameToLayer("PawnView"))
            {
                hitPawnView = true;
                currentHoveredPawnView = hitObject.GetComponentInParent<PawnView>();
            }
            if (!hitTileView && hitLayer == LayerMask.NameToLayer("TileView"))
            {
                hitTileView = true;
                currentHoveredTileView = hitObject.GetComponentInParent<TileView>();
            }
        }
        Vector2Int currentHoveredPosition;
        if (currentHoveredPawnView != null)
        {
            currentHoveredPosition = currentHoveredPawnView.pawn.pos;
        }
        else if (currentHoveredTileView != null)
        {
            currentHoveredPosition = currentHoveredTileView.tile.pos;
        }
        else
        {
            currentHoveredPosition = Globals.PURGATORY;
        }
        // Check if the hovered position or object has changed
        if (currentHoveredPosition != hoveredPosition || currentHoveredObject != hoveredObject)
        {
            // Invoke the event with old and new positions
            OnPositionHovered?.Invoke(hoveredPosition, currentHoveredPosition);

            // Update the hovered position and object
            hoveredPosition = currentHoveredPosition;
            hoveredObject = currentHoveredObject;
        }

        // Update hovered pawn and tile views
        hoveredPawnView = currentHoveredPawnView;
        hoveredTileView = currentHoveredTileView;

        if (Globals.inputActions.Game.Click.triggered)
        {
            HandleClick();
        }
    }

    void HandleClick()
    {
        OnClick?.Invoke(screenPointerPosition, hoveredPosition);
    }

}
