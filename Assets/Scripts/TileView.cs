using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public Tile tile;
    public GameObject model;  // The model whose material color will be changed
    public Transform pawnOrigin;
    
    Renderer modelRenderer;

    public void Initialize(Tile inTile)
    {
        tile = inTile;
        // Change the name of this GameObject to the tile's position
        gameObject.name = $"Tile ({tile.pos.x},{tile.pos.y})";
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
        GetComponent<DebugText>()?.SetText(tile.pos.ToString());
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
        if (!tile.isPassable)
        {
            tileMaterial.color = Color.black;
            return;  // Early return if the tile is not passable
        }
        Color originalColor = tileMaterial.color;
        switch (tile.tileSetup)
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
