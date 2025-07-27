using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickInputManager : MonoBehaviour
{
    public Vector2Int hoveredPosition; // grid position that the pointer is hovered over after hit check
    public Vector2 screenPointerPosition; // raw position on screen

    public GameObject hoveredObject; // first object closest to the camera that the pointer is over
    public bool isOverUI; // is pointer over UI element 
    public PawnView hoveredPawnView; // closest pawnView pointer is over
    public TileView hoveredTileView; // closest tileView pointer is over
    public BoardMakerTile hoveredBoardMakerTile;
    public Dictionary<int, int> layerPriorities;
    
    public bool isUpdating;
    public int resultsCount;
    
    public Action<Vector2Int, bool> OnMouseInput;
    //public Action<Vector2Int> OnClick;

    void Awake()
    {
        layerPriorities = new()
        {
            { LayerMask.NameToLayer("UI"), 0 }, // UI
            { LayerMask.NameToLayer("PawnView"), 1 }, // Highest priority
            { LayerMask.NameToLayer("TileView"), 2 }, // Lower priority
            { LayerMask.NameToLayer("BoardMakerTile"), 3},
            { LayerMask.NameToLayer("Default"), 4 }, // Default priority for other layers
        };
    }
    
    public void SetUpdating(bool inIsUpdating)
    {
        isUpdating = inIsUpdating;
    }
    
    void Update()
    {
        if (!isUpdating) return;
        if (!Application.isFocused) return;
        // get screen pointer position
        screenPointerPosition = Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>();
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPointerPosition,
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        // sort so UI is always on top, then PawnViews then TileViews
        results.Sort((a, b) =>
        {
            if (!a.gameObject || !b.gameObject) return 0;
            
            int layerA = a.gameObject.layer;
            int layerB = b.gameObject.layer;
            int priorityA = layerPriorities.TryGetValue(layerA, out int priority) ? priority : int.MaxValue;
            int priorityB = layerPriorities.TryGetValue(layerB, out int layerPriority) ? layerPriority : int.MaxValue;
            return priorityA.CompareTo(priorityB);
        });
        resultsCount = results.Count;
        bool hitUI = false;
        bool hitPawnView = false;
        bool hitTileView = false;
        bool hitBoardMakerTile = false;
        GameObject currentHoveredObject = results.Count == 0 ? null : results[0].gameObject;
        PawnView currentHoveredPawnView = null;
        TileView currentHoveredTileView = null;
        BoardMakerTile currentHoveredBoardMakerTile = null;
        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            int hitLayer = hitObject.layer;
            if (!hitUI && hitLayer == LayerMask.NameToLayer("UI"))
            {
                hitUI = true;
            }
            if (hitUI)
            {
                
            }
            else
            {
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
                if (!hitBoardMakerTile && hitLayer == LayerMask.NameToLayer("BoardMakerTile"))
                {
                    hitBoardMakerTile = true;
                    currentHoveredBoardMakerTile = hitObject.GetComponentInParent<BoardMakerTile>();
                }
            }
        }
        Vector2Int currentHoveredPosition;
        if (hitUI)
        {
            currentHoveredPosition = Globals.Purgatory;
        }
        else if (currentHoveredPawnView)
        {
            currentHoveredPosition = currentHoveredPawnView.posView;
        }
        else if (currentHoveredTileView)
        {
            currentHoveredPosition = currentHoveredTileView.posView;
        }
        else if (currentHoveredBoardMakerTile)
        {
            currentHoveredPosition = currentHoveredBoardMakerTile.pos;
        }
        else
        {
            currentHoveredPosition = Globals.Purgatory;
        }
        isOverUI = hitUI;
        hoveredPawnView = hitUI ? null : currentHoveredPawnView;
        hoveredTileView = hitUI ? null : currentHoveredTileView;
        // Check if the hovered position or object has changed
        // NOTE: this used to be if (currentHoveredPosition != hoveredPosition || currentHoveredObject != hoveredObject)
        bool clicked = false;
        bool hoverChanged = false;
        if (currentHoveredPosition != hoveredPosition)
        {
            hoverChanged = true;
            // Update the hovered position and object
            hoveredPosition = currentHoveredPosition;
            hoveredObject = currentHoveredObject;
            hoveredBoardMakerTile = currentHoveredBoardMakerTile;
        }
        hoveredBoardMakerTile = currentHoveredBoardMakerTile;
        
        if (!hitUI && Globals.InputActions.Game.Click.triggered)
        {
            clicked = true;
        }

        if (clicked || hoverChanged)
        {
            OnMouseInput?.Invoke(hoveredPosition, clicked);
        }
    }
    
}
