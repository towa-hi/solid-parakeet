using System.Collections.Generic;
using UnityEngine;

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
        // Instantiate entries for each PawnDef
        PawnDef[] pawnDefs = Resources.LoadAll<PawnDef>("Pawn");
        foreach (PawnDef pawnDef in pawnDefs)
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
        GameManager.instance.OnPawnSelectorSelected(tileView, pawnDef);
        Close();
    }
}
