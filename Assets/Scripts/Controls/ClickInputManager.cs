using System;
using System.Collections.Generic;
using System.Linq;
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
    Dictionary<int, int> layerPriorities;

    public event Action<Vector2Int, Vector2Int> OnPositionHovered;
    public event Action<Vector2, Vector2Int> OnClick;

    [SerializeField] bool isUpdating;
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

    public void Initialize(BoardManager boardManager)
    {
        boardManager.OnPhaseChanged += OnPhaseChanged;
        hoveredPosition = Globals.Purgatory;
        hoveredObject = null;
        hoveredPawnView = null;
        hoveredTileView = null;
    }

    public void Initialize(TestBoardManager boardManager)
    {
        boardManager.OnPhaseChanged += OnPhaseChanged;
        hoveredPosition = Globals.Purgatory;
        hoveredObject = null;
        hoveredPawnView = null;
        hoveredTileView = null;
    }
    
    void OnPhaseChanged(IPhase phase)
    {
        switch (phase)
        {
            case UninitializedPhase uninitializedPhase:
                isUpdating = false;
                break;
            case SetupPhase setupPhase:
                isUpdating = true;
                break;
            case WaitingPhase waitingPhase:
                isUpdating = false;
                break;
            case MovePhase movePhase:
                isUpdating = true;
                break;
            case ResolvePhase resolvePhase:
                isUpdating = false;
                break;
            case EndPhase endPhase:
                isUpdating = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }

    public void Reset()
    {
        hoveredPosition = Globals.Purgatory;
        hoveredObject = null;
        hoveredPawnView = null;
        hoveredTileView = null;
        Vector2Int oldHoveredPosition = hoveredPosition;
        OnPositionHovered?.Invoke(oldHoveredPosition, hoveredPosition);
    }

    public int resultsCount = 0;
    void Update()
    {
        if (!isUpdating) return;
        // get screen pointer position
        screenPointerPosition = Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>();
        Ray ray = Camera.main.ScreenPointToRay(screenPointerPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log(hit);
        }
        Debug.DrawRay(ray.origin, ray.direction * 100, Color.red, 1f);
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPointerPosition,
        };
        Debug.Log(eventData.enterEventCamera);
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
        resultsCount = results.Count;
        Debug.Log(resultsCount);
        foreach (RaycastResult result in results)
        {
            Debug.Log(result);
        }
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
        Vector2Int currentHoveredPosition;
        if (currentHoveredPawnView != null)
        {
            currentHoveredPosition = currentHoveredPawnView.pawn.pos;
        }
        else if (currentHoveredTileView != null)
        {
            currentHoveredPosition = currentHoveredTileView.tile.pos;
        }
        else if (currentHoveredBoardMakerTile != null)
        {
            currentHoveredPosition = currentHoveredBoardMakerTile.pos;
        }
        else
        {
            currentHoveredPosition = Globals.Purgatory;
        }
        // Check if the hovered position or object has changed
        // NOTE: this used to be if (currentHoveredPosition != hoveredPosition || currentHoveredObject != hoveredObject)
        if (currentHoveredPosition != hoveredPosition)
        {
            // Invoke the event with old and new positions
            OnPositionHovered?.Invoke(hoveredPosition, currentHoveredPosition);

            // Update the hovered position and object
            hoveredPosition = currentHoveredPosition;
            hoveredObject = currentHoveredObject;
            hoveredBoardMakerTile = currentHoveredBoardMakerTile;
        }
        isOverUI = hitUI;
        // Update hovered pawn and tile views
        hoveredPawnView = currentHoveredPawnView;
        hoveredTileView = currentHoveredTileView;
        hoveredBoardMakerTile = currentHoveredBoardMakerTile;

        if (!isOverUI && Globals.InputActions.Game.Click.triggered)
        {
            OnClick?.Invoke(screenPointerPosition, hoveredPosition);
        }
    }

    public void ForceInvokeOnPositionHovered()
    {
        OnPositionHovered?.Invoke(hoveredPosition, hoveredPosition);
    }

    public void ForceInvokeOnClick()
    {
        OnClick?.Invoke(screenPointerPosition, hoveredPosition);
    }
}
