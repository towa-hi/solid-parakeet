using System;
using UnityEngine;
using PrimeTween;
using UnityEngine.U2D;

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
    static readonly int MainTexID = Shader.PropertyToID("_BaseMap");

    
    public void Initialize(BoardManager boardManager, STile inTile)
    {
        boardManager.OnPhaseChanged += OnPhaseChanged;
        tile = inTile;
        gameObject.name = $"Tile ({tile.pos.x},{tile.pos.y})";
        ShowTile(tile.isPassable);
        arrow.GetComponent<SpriteToMesh>().Activate(arrow.GetComponent<SpriteToMesh>().sprite);
        arrow.SetActive(isArrowed);
        SetRandomTileTexture();
    }

    void SetRandomTileTexture()
    {
        Sprite randomSprite = GameManager.instance.allTileSprites[UnityEngine.Random.Range(0, GameManager.instance.allTileSprites.Count)];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        tileTopRenderer.GetPropertyBlock(block);
        block.SetTexture(MainTexID, randomSprite.texture);
        tileTopRenderer.SetPropertyBlock(block);
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

    Tween currentTween;
    
    public void OnHovered(bool inIsHovered)
    {
        if (!tile.isPassable) return;
        isHovered = inIsHovered;
        SetMeshOutline(isHovered, "HoverOutline");
        PawnView pawnView = GameManager.instance.boardManager.GetPawnViewByPos(tile.pos);
        if (pawnView && pawnView.pawn.player == GameManager.instance.boardManager.player)
        {
            currentTween = Tween.LocalPosition(modelObject.transform, inIsHovered ? new Vector3(0, Globals.HOVEREDHEIGHT, 0) : Vector3.zero, 0.3f, Ease.OutCubic);
        }
        else
        {
            if (modelObject.transform.localPosition != Vector3.zero)
            {
                currentTween = Tween.LocalPosition(modelObject.transform, Vector3.zero, 0.3f, Ease.OutCubic);
            }
        }
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
