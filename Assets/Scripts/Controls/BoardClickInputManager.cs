using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BoardClickInputManager : MonoBehaviour
{
    BoardManager boardManager;
    public TileView currentHoveredTileView;
    public PawnView currentHoveredPawnView;
    public Vector2 screenPointerPosition;
    public GameObject currentHoveredObject;
    public bool isUpdating;
    
    public void Initialize(BoardManager inBoardManager)
    {
        Debug.Log("BoardClickInputManager initialized");
        boardManager = inBoardManager;
        isUpdating = true;
    }

    void Update()
    {
        if (!isUpdating)
        {
            return;
        }
        if (EventSystem.current == null)
        {
            //Debug.Log("EventSystem is not current");
            return;
        }
        screenPointerPosition = Globals.inputActions.Game.PointerPosition.ReadValue<Vector2>();
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPointerPosition
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        results.Sort((a, b) => b.distance.CompareTo(a.distance));
        int layer = 0;
        foreach (var result in results)
        {
            layer += 1;
            
            //Debug.Log($"{layer} - {result.gameObject.name} distance: {result.distance}");
        }
        
        if (IsPointerOverUI(results))
        {
            return;
        }
        bool foundSomething = false;
        if (results.Count == 0)
        {
            currentHoveredObject = null;
            currentHoveredPawnView = null;
            currentHoveredTileView = null;
            return;
        }
        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            //Debug.Log($"hit layer: {hitObject.layer} intended layer: {LayerMask.NameToLayer("PawnView")}");
            if (hitObject.layer == LayerMask.NameToLayer("PawnView"))
            {
                currentHoveredObject = hitObject;
                PawnView pawnView = hitObject.GetComponentInParent<PawnView>();
                currentHoveredPawnView = pawnView;
                currentHoveredTileView = null;
                foundSomething = true;
                //Debug.Log($"CurrentHoveredObject set to: {pawnView.gameObject.name}");
            }
            else if (hitObject.layer == LayerMask.NameToLayer("TileView"))
            {
                currentHoveredObject = hitObject;
                TileView tileView = hitObject.GetComponentInParent<TileView>();
                currentHoveredPawnView = null;
                currentHoveredTileView = tileView;
                foundSomething = true;
                //Debug.Log($"CurrentHoveredObject set to: {tileView.gameObject.name}");
            }
        }

        if (!foundSomething)
        {
            currentHoveredObject = null;
            currentHoveredPawnView = null;
            currentHoveredTileView = null;
        }
    }
    
    bool IsPointerOverUI(List<RaycastResult> results)
    {

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.layer == LayerMask.NameToLayer("UI"))
            {
                return true;
            }
        }
        return false;
    }
    
    
}
