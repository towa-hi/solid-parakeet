using System;
using UnityEngine;
using UnityEngine.U2D;

public class PawnView : MonoBehaviour
{
    public GameObject model;
    public GameObject cube;
    public GameObject plane;
    public GameObject billboard;
    
    public SpriteAtlas symbols;
    public SpriteRenderer symbolRenderer;
    
    public Pawn pawn;

    public MeshRenderer billboardRenderer;
    public MeshRenderer planeRenderer;
    
    public bool isSelected = false;
    public bool isHovered = false;
    void OnDestroy()
    {
        
    }

    void Start()
    {
        GameManager.instance.boardManager.OnPawnModified += OnPawnModified;
    }

    void OnPawnModified(PawnChanges pawnChanges)
    {
        if (pawnChanges.pawn.pawnId != pawn.pawnId) return;
        if (pawn.isAlive)
        {
            transform.position = GameManager.instance.boardManager.GetTileView(pawn.pos).pawnOrigin.position;
        }
        else
        {
            transform.position = GameManager.instance.boardManager.purgatory.position;
        }
    }

    public virtual void Initialize(Pawn inPawn, TileView tileView)
    {
        pawn = inPawn;
        if (pawn.def != null)
        {
            gameObject.name = $"{pawn.player} Pawn {pawn.def.pawnName} {pawn.pawnId}";
            GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
            DisplaySymbol(pawn.def.icon);
        }
        else
        {
            gameObject.name = $"{pawn.player} Pawn Unknown {pawn.pawnId}";
            billboard.gameObject.SetActive(false);
        }
        switch (inPawn.player)
        {
            case Player.RED:
                SetCubeColor(Color.red);
                break;
            case Player.BLUE:
                SetCubeColor(Color.blue);
                break;
        }
        if (tileView == null)
        {
            transform.position = GameManager.instance.boardManager.purgatory.position;
        }
        else
        {
            transform.position = tileView.pawnOrigin.position;
        }

        PawnChanges pawnChanges = new()
        {
            pawn = pawn,
            hasMovedChanged = true,
            isAliveChanged = true,
            isSetupChanged = true,
            isVisibleToOpponentChanged = true,
            posChanged = true,
        };
        OnPawnModified(pawnChanges);
    }

    protected void DisplaySymbol(Sprite sprite)
    {
        //Debug.Log(symbols.spriteCount);
        if (sprite == null)
        {
            Debug.Log("wew");
        }
        symbolRenderer.sprite = sprite;
    }

    protected void SetCubeColor(Color color)
    {
        Renderer cubeRenderer = cube.GetComponent<Renderer>();
        cubeRenderer.material = new(cubeRenderer.material)
        {
            color = color
        };
    }
    
    uint currentRenderingLayerMask;
    
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
        // Ensure that the base layer is always included
        currentRenderingLayerMask |= (1u << 0);

        // Apply the updated rendering layer mask to the renderer
        billboardRenderer.renderingLayerMask = currentRenderingLayerMask;
        planeRenderer.renderingLayerMask = currentRenderingLayerMask;
    }

    public void OnHovered(bool inIsHovered)
    {
        isHovered = inIsHovered;
        SetMeshOutline(isHovered, "HoverOutline");
    }

    public void SetSelect(bool inIsSelected)
    {
        isSelected = inIsSelected;
        SetMeshOutline(isSelected, "SelectOutline");
    }

    public void OnHighlight(bool inIsHighlighted)
    {
        SetMeshOutline(inIsHighlighted, "Fill");
    }
}
