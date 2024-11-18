using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public Tile tile;
    public GameObject model;  // The model whose material color will be changed
    public GameObject floor;
    public Transform pawnOrigin;
    public BoardManager boardManager;
    
    Renderer modelRenderer;
    Renderer floorRenderer;

    void Awake()
    {
        modelRenderer = model.GetComponentInChildren<Renderer>();
        if (modelRenderer == null)
        {
            Debug.Log("wtf");
        }
        floorRenderer = floor.GetComponent<Renderer>();
        floorRenderer.enabled = false;
    }

    void OnDestroy()
    {
        
    }
    
    public void Initialize(Tile inTile, BoardManager inBoardManager)
    {
        tile = inTile;
        boardManager = inBoardManager;
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
            return;
        }
        Color originalColor = tileMaterial.color;
        switch (tile.setupPlayer)
        {
            case Player.RED:
                tileMaterial.color = Color.red;
                break;
            case Player.BLUE:
                tileMaterial.color = Color.blue;
                break;
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

    void OnClicked(Vector2 mousePos)
    {
        Debug.Log($"Tileview {gameObject.name} clicked");
        
        //GameManager.instance.boardManager.OnTileClicked(this);

    }

    public bool IsTileInteractableDuringSetup()
    {
        if (tile.setupPlayer == Player.NONE)
        {
            return false;
        }

        if (boardManager.player == Player.NONE)
        {
            throw new Exception("boardManager.player cannot be NONE");
        }
        return tile.setupPlayer == boardManager.player;
    }
}
