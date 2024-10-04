using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // For LayoutRebuilder
using System.Linq;

public class PawnSelector : MonoBehaviour
{
    public GameObject entryPrefab;
    public List<PawnSelectorEntry> entryList = new();

    Vector2 pos;
    TileView tileView;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    void Awake()
    {
        // Since the GameObject starts inactive, we use Awake() instead of Start()
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas == null)
        {
            Debug.LogError("No parent Canvas found for PawnSelector.");
        }

        // Populate entries once when the script is loaded
        PopulateEntries();
    }

    void PopulateEntries()
    {
        // Clear existing entries if any
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        entryList.Clear();

        // Load all PawnDefs
        PawnDef[] pawnDefs = Resources.LoadAll<PawnDef>("Pawn");

        // Sort pawnDefs by PawnDef.power
        PawnDef[] sortedPawnDefs = pawnDefs.OrderBy(p => p.power).ToArray();

        // Add an empty PawnSelectorEntry at the top
        GameObject emptyEntryObject = Instantiate(entryPrefab, transform);
        PawnSelectorEntry emptyEntry = emptyEntryObject.GetComponent<PawnSelectorEntry>();
        emptyEntry.Initialize(null, OnSelected);
        entryList.Add(emptyEntry);

        // Instantiate entries for each sorted PawnDef
        foreach (PawnDef pawnDef in sortedPawnDefs)
        {
            GameObject entryObject = Instantiate(entryPrefab, transform);
            PawnSelectorEntry entry = entryObject.GetComponent<PawnSelectorEntry>();
            entry.Initialize(pawnDef, OnSelected);
            entryList.Add(entry);
        }

        // Force update the layout to get the correct size later
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    public void OpenAndInitialize(TileView inTileView)
    {
        Clear();

        gameObject.SetActive(true);
        tileView = inTileView;

        // Get the screen position of the TileView's world position
        Vector3 tileScreenPosition = Camera.main.WorldToScreenPoint(tileView.transform.position);

        // Convert screen position to local position in the UI Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform, tileScreenPosition, parentCanvas.worldCamera, out pos);

        // Adjust pivot and position to keep the popup on screen
        AdjustPivotAndPositionToKeepOnScreen();

    }

    void Clear()
    {
        tileView = null;
        pos = Vector2.zero;
        gameObject.SetActive(false);
    }

    void Close()
    {
        Clear();
    }

    void OnSelected(PawnDef pawnDef)
    {
        GameManager.instance.OnSetupPawnSelectorSelected(tileView, pawnDef);
        Close();
    }
private void AdjustPivotAndPositionToKeepOnScreen()
{
    // Ensure the layout is up to date
    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

    // Get the size of the popup
    Vector2 size = rectTransform.rect.size;

    // Get the canvas RectTransform
    RectTransform canvasRectTransform = parentCanvas.transform as RectTransform;

    // Calculate the boundaries of the canvas
    float canvasWidth = canvasRectTransform.rect.width;
    float canvasHeight = canvasRectTransform.rect.height;

    // Start with the default pivot (0,1) - top-left
    Vector2 pivot = new Vector2(0, 1);
    Vector2 adjustedPosition = pos;

    // Adjust pivot and position for horizontal edges
    if (pos.x + size.x > canvasWidth / 2)
    {
        // Popup would go off the right edge
        // Adjust pivot to top-right
        pivot.x = 1;
        // Adjust position: align the pivot (right edge) with the click position
        adjustedPosition.x = pos.x;
    }
    else if (pos.x < -canvasWidth / 2)
    {
        // Popup would go off the left edge
        // Keep pivot at 0, but adjust position to the left edge
        adjustedPosition.x = -canvasWidth / 2;
    }
    else
    {
        // Default behavior: align the pivot (left edge) with the click position
        adjustedPosition.x = pos.x;
    }

    // Adjust pivot and position for vertical edges
    if (pos.y - size.y < -canvasHeight / 2)
    {
        // Popup would go off the bottom edge
        // Adjust pivot to bottom
        pivot.y = 0;
        // Adjust position: align the pivot (bottom edge) with the click position
        adjustedPosition.y = pos.y;
    }
    else if (pos.y > canvasHeight / 2)
    {
        // Popup would go off the top edge
        // Keep pivot at 1, but adjust position to the top edge
        adjustedPosition.y = canvasHeight / 2;
    }
    else
    {
        // Default behavior: align the pivot (top edge) with the click position
        adjustedPosition.y = pos.y;
    }

    // Apply the pivot and position adjustments
    rectTransform.pivot = pivot;
    rectTransform.anchoredPosition = adjustedPosition;
}


}
