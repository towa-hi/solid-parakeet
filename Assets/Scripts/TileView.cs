using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public TileData tileData;
    public GameObject model;  // The model whose material color will be changed

    private Renderer modelRenderer;

    void Start()
    {

    }

    public void Initialize(TileData newTileData)
    {        
        // Ensure the model has a Renderer component
        if (model != null)
        {
            modelRenderer = model.GetComponent<Renderer>();
            if (modelRenderer == null)
            {
                Debug.LogError("Renderer not found on model!");
            }
        }
        else
        {
            Debug.LogError("Model is not assigned in TileView!");
        }
        tileData = newTileData;
        GetComponent<DebugText>()?.SetText(tileData.pos.ToString());

        // Update the material color based on the tile setup
        UpdateModelColor();
    }

    // Method to update the material color based on TileSetup
    void UpdateModelColor()
    {
        if (modelRenderer == null)
        {
            Debug.LogError("Model renderer is missing!");
            return;  // Exit if no renderer is found
        }

        // Use material to ensure we're working with an instance of the material
        Material tileMaterial = modelRenderer.material;

        if (!tileData.isPassable)
        {
            tileMaterial.color = Color.black;
            return;  // Early return if the tile is not passable
        }

        // Change color based on tile setup
        Util.Log("setting color");
        Color originalColor = tileMaterial.color;
        switch (tileData.tileSetup)
        {
            case TileSetup.RED:
                tileMaterial.color = Color.red;
                break;
            case TileSetup.BLUE:
                tileMaterial.color = Color.blue;
                break;
            case TileSetup.NONE:
            default:
                tileMaterial.color = originalColor;  // Default color
                break;
        }
    }
}
