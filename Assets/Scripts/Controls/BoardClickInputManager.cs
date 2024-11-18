using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class BoardClickInputManager : MonoBehaviour
{
    public Vector2Int currentHoveredPosition = new Vector2Int(-1, -1);
    public TileView currentHoveredTileView;
    public PawnView currentHoveredPawnView;
    public Vector2 screenPointerPosition;

    public TileView currentClickedTileView;
    public PawnView currentClickedPawnView;
    
    public bool isUpdating;
    public bool isPointerOverUI;

    public event Action<Vector2Int> OnPositionClicked;
    public event Action<Vector2Int> OnPositionHovered;
    
    public void Initialize()
    {
        Debug.Log("BoardClickInputManager initialized");
        isUpdating = true;
    }

    void Update()
    {
        // Reset hovered objects at the start
        currentHoveredPawnView = null;
        currentHoveredTileView = null;

        if (!isUpdating)
        {
            return;
        }

        bool foundSomething = false;

        screenPointerPosition = Globals.inputActions.Game.PointerPosition.ReadValue<Vector2>();
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPointerPosition
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        isPointerOverUI = IsPointerOverUI(results);

        if (isPointerOverUI)
        {
            // When over UI, reset currentHoveredPosition if it was not already (-1, -1)
            if (currentHoveredPosition != new Vector2Int(-1, -1))
            {
                currentHoveredPosition = new Vector2Int(-1, -1);
                OnPositionHovered?.Invoke(currentHoveredPosition);
            }
            return;
        }

        // Define layer priorities
        Dictionary<int, int> layerPriorities = new Dictionary<int, int>
        {
            { LayerMask.NameToLayer("PawnView"), 0 }, // Highest priority
            { LayerMask.NameToLayer("TileView"), 1 }, // Lower priority
            { LayerMask.NameToLayer("Default"), 2 }   // Default priority for other layers
        };

        // Sort results based on layer priority
        results.Sort((a, b) =>
        {
            int layerA = a.gameObject.layer;
            int layerB = b.gameObject.layer;

            int priorityA = layerPriorities.ContainsKey(layerA) ? layerPriorities[layerA] : int.MaxValue;
            int priorityB = layerPriorities.ContainsKey(layerB) ? layerPriorities[layerB] : int.MaxValue;

            return priorityA.CompareTo(priorityB);
        });

        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            int hitLayer = hitObject.layer;

            if (hitLayer == LayerMask.NameToLayer("PawnView"))
            {
                currentHoveredPawnView = hitObject.GetComponentInParent<PawnView>();
                currentHoveredTileView = null;
                foundSomething = true;

                Vector2Int newPosition = currentHoveredPawnView.pawn.pos;
                if (currentHoveredPosition != newPosition)
                {
                    currentHoveredPosition = newPosition;
                    OnPositionHovered?.Invoke(currentHoveredPosition);
                }
                break; // Found the highest priority object
            }
            else if (hitLayer == LayerMask.NameToLayer("TileView"))
            {
                if (!foundSomething)
                {
                    currentHoveredTileView = hitObject.GetComponentInParent<TileView>();
                    currentHoveredPawnView = null;
                    foundSomething = true;

                    Vector2Int newPosition = currentHoveredTileView.tile.pos;
                    if (currentHoveredPosition != newPosition)
                    {
                        currentHoveredPosition = newPosition;
                        OnPositionHovered?.Invoke(currentHoveredPosition);
                    }
                    // Continue in case a higher priority object is found
                }
            }
            // Continue processing in case a higher priority object is found
        }

        if (!foundSomething)
        {
            // If no valid object is found, reset currentHoveredPosition if it was not already (-1, -1)
            if (currentHoveredPosition != new Vector2Int(-1, -1))
            {
                currentHoveredPosition = new Vector2Int(-1, -1);
                OnPositionHovered?.Invoke(currentHoveredPosition);
            }
        }

        if (Globals.inputActions.Game.Click.triggered)
        {
            HandleClick();
        }
    }
    
    bool IsPointerOverUI(List<RaycastResult> results)
    {
        return results.Any(result => result.gameObject.layer == LayerMask.NameToLayer("UI"));
    }

    void HandleClick()
    {
        if (isPointerOverUI)
        {
            return;
        }
        if (currentHoveredPawnView)
        {
            Vector2Int pos = currentHoveredPawnView.pawn.pos;
            Debug.Log($"Clicked pawn at {pos}");
            currentClickedPawnView = currentHoveredPawnView;
            OnPositionClicked?.Invoke(pos);
            // Handle pawn click
        }
        else if (currentHoveredTileView)
        {
            Vector2Int pos = currentHoveredTileView.tile.pos;
            Debug.Log($"Clicked tile at {pos}");
            OnPositionClicked?.Invoke(pos);
            // Handle tile click
        }
        else
        {
            // Optionally handle clicks on empty space
            Debug.Log("Clicked on empty space");
            OnPositionClicked(new Vector2Int(-1, -1));
        }
    }
}
