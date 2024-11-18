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

    void OnPawnModified(Pawn somePawn)
    {
        if (somePawn != pawn) return;
        if (pawn.isAlive)
        {
            transform.position = GameManager.instance.boardManager.GetTileView(pawn.pos).pawnOrigin.position;
        }
        else
        {
            transform.position = GameManager.instance.boardManager.purgatory.position;
        }
    }

    public void OnPositionClicked(Vector2Int pos)
    {
        isSelected = pawn.pos == pos;
        SetMeshOutline(isSelected, "SelectOutline");
    }
    
    public void OnPositionHovered(Vector2Int pos)
    {
        isHovered = pawn.pos == pos;
        SetMeshOutline(isHovered, "HoverOutline");
        if (isHovered)
        {
            Debug.Log($"{gameObject.name}: OnPositionHovered");
        }
    }

    uint currentRenderingLayerMask;
    
    void SetMeshOutline(bool enable, string outlineType)
    {
        uint outlineLayer = 0;
        switch (outlineType)
        {
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

        planeRenderer.renderingLayerMask = currentRenderingLayerMask;
        billboardRenderer.renderingLayerMask = currentRenderingLayerMask;
    }

    public virtual void Initialize(Pawn inPawn, TileView tileView)
    {
        pawn = inPawn;
        gameObject.name = $"Pawn {pawn.def.pawnName} {pawn.pawnId}";
        if (pawn.def != null)
        {
            GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
            DisplaySymbol(Globals.pawnSprites[inPawn.def.pawnName]);
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
        
    }


    public void RemoveFromPurgatory(Vector2Int pos)
    {
        pawn.SetAlive(true, pos);
        transform.position = GameManager.instance.boardManager.GetTileView(pos).pawnOrigin.position;
    }

    protected void DisplaySymbol(string index)
    {
        
        Sprite sprite = symbols.GetSprite(index);
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

    public void OnClick()
    {
        Debug.Log($"pawnView {gameObject.name} clicked");
    }

    public void OnHover()
    {
        throw new NotImplementedException();
    }
    
    
}
