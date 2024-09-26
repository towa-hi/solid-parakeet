using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public Tile tile;
    public GameObject model;  // The model whose material color will be changed
    public GameObject floor;
    public Transform pawnOrigin;
    
    Renderer modelRenderer;
    Renderer floorRenderer;
    Clickable clickable;

    void Awake()
    {
        modelRenderer = model.GetComponentInChildren<Renderer>();
        if (modelRenderer == null)
        {
            Debug.Log("wtf");
        }
        floorRenderer = floor.GetComponent<Renderer>();
        clickable = floor.GetComponent<Clickable>();
        floorRenderer.enabled = false;
        clickable.OnHoverEnter += OnHoverEnter;
        clickable.OnHoverExit += OnHoverExit;
    }

    void OnDestroy()
    {
        
    }
    
    public void Initialize(Tile inTile)
    {
        tile = inTile;
        // Change the name of this GameObject to the tile's position
        gameObject.name = $"Tile ({tile.pos.x},{tile.pos.y})";

        GetComponent<DebugText>()?.SetText(tile.pos.ToString());
        // Update the material color based on the tile setup
        SetModelColorToTeam();
    }

    // Method to update the material color based on TileSetup
    void SetModelColorToTeam()
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

    void OnHoverEnter()
    {
        floorRenderer.enabled = true;
    }

    void OnHoverExit()
    {
        floorRenderer.enabled = false;
    }
}
