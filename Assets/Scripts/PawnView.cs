using System.Collections;
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
    public bool isMoving = false;
    
    // Reference to the current movement coroutine
    private Coroutine moveCoroutine;
    
    void Start()
    {
        GameManager.instance.boardManager.OnPawnModified += OnPawnModified;
    }

    void OnPawnModified(PawnChanges pawnChanges)
    {
        if (pawnChanges.pawn.pawnId != pawn.pawnId) return;

        // Get the target position
        Vector3 targetPosition;
        if (pawn.isAlive)
        {
            targetPosition = GameManager.instance.boardManager.GetTileView(pawn.pos).pawnOrigin.position;
        }
        else
        {
            targetPosition = GameManager.instance.boardManager.purgatory.position;
        }
        
        // Stop any existing movement coroutine
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        // Start the movement coroutine to smoothly move to the target position
        moveCoroutine = StartCoroutine(MoveToPosition(targetPosition, Globals.PAWNMOVEDURATION));

        if (pawnChanges.isVisibleToOpponentChanged)
        {
            DisplaySymbol(pawn.def.icon);
        }
    }

    public void MoveView(Vector2Int pos)
    {
        Vector3 targetPosition = GameManager.instance.boardManager.GetTileView(pos).pawnOrigin.position;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        // Start the movement coroutine to smoothly move to the target position
        moveCoroutine = StartCoroutine(MoveToPosition(targetPosition, Globals.PAWNMOVEDURATION));
    }

    IEnumerator MoveToPosition(Vector3 targetPosition, float duration)
    {
        isMoving = true;
        GameManager.instance.boardManager.movingPawnsCount++;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // Lerp from startPosition to targetPosition over duration
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the final position is set
        transform.position = targetPosition;
        isMoving = false;
        GameManager.instance.boardManager.movingPawnsCount--;
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
                SetColor(Color.red);
                break;
            case Player.BLUE:
                SetColor(Color.blue);
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

        // Initialize current position as the starting position
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
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

    void DisplaySymbol(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.Log("Sprite is null.");
        }
        symbolRenderer.sprite = sprite;
    }

    void SetColor(Color color)
    {
        planeRenderer.material = new Material(planeRenderer.material)
        {
            color = color,
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
