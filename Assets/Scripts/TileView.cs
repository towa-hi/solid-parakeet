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

    public bool isSelected = false;
    public bool isHovered = false;
    
    void Awake()
    {
        modelRenderer = model.GetComponentInChildren<Renderer>();
        if (modelRenderer == null)
        {
            Debug.Log("wtf");
        }
        floorRenderer = floor.GetComponent<Renderer>();
    }



    public void OnPositionClicked(Vector2Int pos)
    {
        //SetMeshOutline(tile.pos == pos);
        isSelected = tile.pos == pos;
        SetMeshOutline(isHovered, "SelectOutline");
        if (isSelected)
        {
            Debug.Log($"{gameObject.name}: OnPositionClicked");
        }
    }
    
    public void OnPositionHovered(Vector2Int pos)
    {
        isHovered = tile.pos == pos;
        SetMeshOutline(isHovered, "HoverOutline");
        if (isHovered)
        {
            Debug.Log($"{gameObject.name}: OnPositionHovered");
        }
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

    uint currentRenderingLayerMask;
    void SetMeshOutline(bool enable, string outlineType)
    {
        uint outlineLayer = 0;
        switch (outlineType)
        {
            case "Fill":
                outlineLayer = (1 << 6);
                break;
            case "HoverOutline":
                outlineLayer = (1 << 7);
                break;
            case "SelectOutline":
                outlineLayer = (1 << 8);
                break;
        }

        if (enable)
        {
            currentRenderingLayerMask |= outlineLayer;
        }
        else
        {
            currentRenderingLayerMask &= ~outlineLayer;
        }
        currentRenderingLayerMask |= (1 << 0);

        floorRenderer.renderingLayerMask = currentRenderingLayerMask;
        floorRenderer.renderingLayerMask = currentRenderingLayerMask;
    }
    
    public void OnHovered(bool isHovered)
    {
        this.isHovered = isHovered;
        SetMeshOutline(isHovered, "HoverOutline");
    }
}
