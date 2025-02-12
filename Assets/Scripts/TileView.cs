using System;
using UnityEngine;
using PrimeTween;
using UnityEngine.Serialization;
using UnityEngine.U2D;

public class TileView : MonoBehaviour
{
    public STile tile;
    public Transform pawnOrigin;
    public GameObject floorObject;
    public GameObject modelObject;
    public GameObject arrow;
    public Transform fallOrigin;
    
    public GameObject squareTile;
    public Renderer squareTileTopRenderer;
    public Renderer squareTileSurfaceRenderer;
    
    public GameObject hexTile;
    public Renderer hexTileTopRenderer;
    public Renderer hexTileSurfaceRenderer;
    
    Renderer tileTopRenderer;
    Renderer tileSurfaceRenderer;

    public Color baseColor; 
    public Color redColor;
    public Color blueColor;
    [SerializeField] bool isHex;
    
    [SerializeField] bool isShowing;
    [SerializeField] bool isHovered;
    [SerializeField] bool isHighlighted;
    [SerializeField] bool isArrowed;
    [SerializeField] bool isSelected;
    
    [SerializeField] TweenSettings<Vector3> fallSettings;

    Vector3 hoveredModelPosition = new Vector3(0, Globals.HoveredHeight, 0);
    Vector3 selectedModelPosition = new Vector3(0, Globals.SelectedHoveredHeight, 0);
    uint currentRenderingLayerMask;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int MainTexID = Shader.PropertyToID("_BaseMap");

    
    public void Initialize(BoardManager boardManager, STile inTile, bool inIsHex)
    {
        isHex = inIsHex;
        if (isHex)
        {
            tileTopRenderer = hexTileTopRenderer;
            tileSurfaceRenderer = hexTileSurfaceRenderer;
            squareTile.SetActive(false);
            squareTileSurfaceRenderer.gameObject.SetActive(false);
        }
        else
        {
            tileTopRenderer = squareTileTopRenderer;
            tileSurfaceRenderer = squareTileSurfaceRenderer;
            hexTile.SetActive(false);
            hexTileSurfaceRenderer.gameObject.SetActive(false);
        }
        boardManager.OnPhaseChanged += OnPhaseChanged;
        tile = inTile;
        gameObject.name = $"Tile ({tile.pos.x},{tile.pos.y})";
        ShowTile(tile.isPassable);
        arrow.GetComponent<SpriteToMesh>().Activate(arrow.GetComponent<SpriteToMesh>().sprite);
        arrow.SetActive(isArrowed);
    }
    
    public void FallingAnimation(float delay)
    {
        if (tile.isPassable)
        {
            //Debug.Log($"starting FallingAnimation on {tile.pos}");
            Sequence.Create()
                .ChainCallback(() => ShowTile(false))
                .ChainDelay(delay)
                .ChainCallback(() => ShowTile(tile.isPassable))
                .Chain(Tween.LocalPosition(modelObject.transform, fallSettings));
        }

    }
    
    void OnPhaseChanged(IPhase phase)
    {
        switch (phase)
        {
            case UninitializedPhase uninitializedPhase:
                break;
            case SetupPhase setupPhase:
                SetTopColorBySetupTeam();
                break;
            case WaitingPhase waitingPhase:
                break;
            case MovePhase movePhase:
                ResetTopColor();
                break;
            case ResolvePhase resolvePhase:
                break;
            case EndPhase endPhase:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }
    
    void SetTopColorBySetupTeam()
    {
        MaterialPropertyBlock block = new();
        tileTopRenderer.GetPropertyBlock(block);
        switch ((Team)tile.setupTeam)
        {
            case Team.NONE:
                block.SetColor(BaseColorID, baseColor);
                break;
            case Team.RED:
                block.SetColor(BaseColorID, redColor);
                break;
            case Team.BLUE:
                block.SetColor(BaseColorID, blueColor);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        tileTopRenderer.SetPropertyBlock(block);
    }

    void ResetTopColor()
    {
        MaterialPropertyBlock block = new();
        tileTopRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorID, baseColor);
        tileTopRenderer.SetPropertyBlock(block);
    }

    void SetMeshOutline(bool enable, string outlineType)
    {
        uint outlineLayer = 0;
        switch (outlineType)
        {
            case "Fill":
                outlineLayer = (1u << 6);
                break;
            case "HoverOutline":
                outlineLayer = (1u << 7);
                break;
            case "SelectOutline":
                outlineLayer = (1u << 8);
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
        currentRenderingLayerMask |= (1u << 0);
        tileSurfaceRenderer.renderingLayerMask = currentRenderingLayerMask;
    }
    
    void ShowTile(bool inIsShowing)
    {
        isShowing = inIsShowing;
        modelObject.SetActive(isShowing);
        floorObject.SetActive(isShowing);
    }

    Tween currentTween;
    
    public void OnHovered(bool inIsHovered)
    {
        if (!tile.isPassable) return;
        isHovered = inIsHovered;
        SetMeshOutline(isHovered, "HoverOutline");
    }
    
    public void OnSelect(bool inIsSelected)
    {
        isSelected = inIsSelected;
    }
    
    // Add the OnHighlight method
    public void OnHighlight(bool inIsHighlighted)
    {
        isHighlighted = inIsHighlighted;
        SetMeshOutline(isHighlighted, "Fill");
    }

    public void OnArrow(bool inIsArrowed)
    {
        isArrowed = inIsArrowed;
        arrow.SetActive(isArrowed);
    }

    public void Elevate(float height)
    {
        Vector3 destination = new Vector3(0, height, 0);
        if (modelObject.transform.position != destination)
        {
            //Debug.Log($"Elevate {tile.pos} {destination}");
            currentTween.Stop();
            currentTween = Tween.LocalPosition(modelObject.transform, destination, 0.3f, Ease.OutCubic);
        }
        else
        {
            //Debug.Log($"Elevate didn't change at {tile.pos}");
        }
    }
}
