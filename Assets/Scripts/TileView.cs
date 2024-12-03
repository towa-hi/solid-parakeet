using System;
using UnityEngine;
using PrimeTween;

public class TileView : MonoBehaviour
{
    public STile tile;
    public Transform pawnOrigin;
    public GameObject floorObject;
    public GameObject modelObject;
    public GameObject arrow;
    public Transform fallOrigin;
    
    public Renderer tileTopRenderer;
    public Renderer cubeRenderer;
    public Renderer floorRenderer;

    public Color baseColor; 
    public Color redColor;
    public Color blueColor;
    
    [SerializeField] bool isShowing;
    [SerializeField] bool isHovered;
    [SerializeField] bool isHighlighted;
    [SerializeField] bool isArrowed;
    
    [SerializeField] TweenSettings<Vector3> fallSettings;
    
    
    uint currentRenderingLayerMask;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    

    
    public void Initialize(BoardManager boardManager, STile inTile)
    {
        boardManager.OnPhaseChanged += OnPhaseChanged;
        tile = inTile;
        gameObject.name = $"Tile ({tile.pos.x},{tile.pos.y})";
        ShowTile(tile.isPassable);
    }

    public void FallingAnimation(float delay)
    {
        Debug.Log($"starting FallingAnimation on {tile.pos}");
        Sequence.Create()
            .ChainCallback(() => ShowTile(false))
            .ChainDelay(delay)
            .ChainCallback(() => ShowTile(tile.isPassable))
            .Chain(Tween.LocalPosition(modelObject.transform, fallSettings));
    }
    
    void OnPhaseChanged(IPhase phase)
    {
        switch (phase)
        {
            case UninitializedPhase uninitializedPhase:
                break;
            case SetupPhase setupPhase:
                SetTopColorBySetupPlayer();
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
    
    void SetTopColorBySetupPlayer()
    {
        MaterialPropertyBlock block = new();
        tileTopRenderer.GetPropertyBlock(block);
        switch ((Player)tile.setupPlayer)
        {
            case Player.NONE:
                block.SetColor(BaseColorID, baseColor);
                break;
            case Player.RED:
                block.SetColor(BaseColorID, redColor);
                break;
            case Player.BLUE:
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
        floorRenderer.renderingLayerMask = currentRenderingLayerMask;
    }
    
    void ShowTile(bool inIsShowing)
    {
        isShowing = inIsShowing;
        modelObject.SetActive(isShowing);
        floorObject.SetActive(isShowing);
    }
    
    public void OnHovered(bool inIsHovered)
    {
        isHovered = inIsHovered;
        SetMeshOutline(isHovered, "HoverOutline");
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
}
