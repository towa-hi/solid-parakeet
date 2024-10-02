using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Add this namespace for LINQ methods

public class PawnSelector : MonoBehaviour
{
    public GameObject entryPrefab;
    public List<PawnSelectorEntry> entryList = new();

    Vector2 pos;
    TileView tileView;

    void Start()
    {
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
        // Initialize with null PawnDef or handle appropriately in Initialize method
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
    }

    public void OpenAndInitialize(TileView inTileView)
    {
        Clear();
        tileView = inTileView;

        // Get the screen position of the TileView's world position
        Vector3 tileScreenPosition = Camera.main.WorldToScreenPoint(tileView.transform.position);

        // Get the RectTransform of the Canvas
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("No parent Canvas found for PawnSelector.");
            return;
        }

        RectTransform canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
        if (canvasRectTransform == null)
        {
            Debug.LogError("No RectTransform found on the Canvas.");
            return;
        }

        // Convert screen position to local position in the UI Canvas
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, tileScreenPosition, null, out pos))
        {
            // Set the RectTransform's anchored position to the calculated pos
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = pos;
        }
        else
        {
            Debug.LogError("Failed to convert screen point to local point in canvas.");
        }

        gameObject.SetActive(true);
    }

    void Clear()
    {
        tileView = null;
        pos = Vector2.zero;
        gameObject.SetActive(false);
        // Optionally, clear the entries
        // foreach (Transform child in transform)
        // {
        //     Destroy(child.gameObject);
        // }
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
}
