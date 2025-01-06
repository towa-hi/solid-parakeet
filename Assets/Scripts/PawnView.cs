using System;
using System.Collections;
using PrimeTween;
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

    bool displayFloorSymbol;
    
    public MeshRenderer billboardRenderer;
    public MeshRenderer planeRenderer;
    public Shatter shatterEffect;
    public bool isSelected;
    public bool isHovered;
    public bool isMoving;

    public GameObject badge;
    public SpriteRenderer badgeSpriteRenderer;
    public SpriteRenderer badgeBackgroundRenderer;
    public Color redColor;
    public Color blueColor;
    
    public Collider pointerCollider;
    // Reference to the current movement coroutine
    Coroutine moveCoroutine;
    
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    
    public void Initialize(Pawn inPawn, TileView tileView)
    {
        pawn = inPawn;
        gameObject.name = $"{pawn.player} Pawn {pawn.def.pawnName} {Globals.ShortGuid(pawn.pawnId)}";
        UpdateSprite();
        //GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
        DisplaySymbol(pawn.def.icon);
        switch (inPawn.player)
        {
            case Player.RED:
                SetColor(redColor);
                break;
            case Player.BLUE:
                SetColor(blueColor);
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
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        badge.SetActive(PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
    }
    
    public void SyncState(SPawn state)
    {
        PawnChanges pawnChanges = new()
        {
            pawn = pawn,
        };
        if (pawn.pos != state.pos)
        {
            pawnChanges.posChanged = true;
            pawn.pos = state.pos;
        }
        if (pawn.isSetup != state.isSetup)
        {
            pawnChanges.isSetupChanged = true;
            pawn.isSetup = state.isSetup;
        }
        if (pawn.isAlive != state.isAlive)
        {
            pawnChanges.isAliveChanged = true;
            pawn.isAlive = state.isAlive;
        }
        if (pawn.hasMoved != state.hasMoved)
        {
            pawnChanges.hasMovedChanged = true;
            pawn.hasMoved = state.hasMoved;
        }
        if (pawn.isVisibleToOpponent != state.isVisibleToOpponent)
        {
            pawnChanges.isVisibleToOpponentChanged = true;
            pawn.isVisibleToOpponent = state.isVisibleToOpponent;
            if (state.isVisibleToOpponent)
            {
                pawn.def = state.def.ToUnity();
            }
        }
    }

    public void UpdateViewPosition()
    {
        Vector3 targetPosition;
        if (pawn.isAlive)
        {
            targetPosition = GameManager.instance.boardManager.GetTileViewByPos(pawn.pos).pawnOrigin.position;
        }
        else
        {
            targetPosition = GameManager.instance.boardManager.purgatory.position;
        }
        transform.position = targetPosition;
    }
    
    public void RevealPawn(SPawn sPawn)
    {
        Debug.Log($"REVEALPAWN {gameObject.name}");
        pawn.def = sPawn.def.ToUnity();
        DisplaySymbol(pawn.def.icon);
        UpdateSprite();
    }

    void UpdateSprite()
    {
        Sprite displaySprite;
        if (pawn.player == Player.RED)
        {
            displaySprite = pawn.def.redSprite;
        }
        else
        {
            displaySprite = pawn.def.blueSprite;
        }
        
        billboard.GetComponent<SpriteToMesh>().Activate(displaySprite);
    }
    
    public IEnumerator ArcToPosition(Vector3 targetPosition, float duration, float arcHeight)
    {
        isMoving = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // Calculate the normalized time (0 to 1)
            float t = elapsedTime / duration;
            t = Globals.EaseOutQuad(t);
            
            // Interpolate position horizontally
            Vector3 horizontalPosition = Vector3.Lerp(startPosition, targetPosition, t);

            // Calculate vertical arc using a parabolic equation
            float verticalOffset = arcHeight * (1 - Mathf.Pow(2 * t - 1, 2)); // Parabolic equation: a(1 - (2t - 1)^2)
            horizontalPosition.y += verticalOffset;

            // Apply the calculated position
            transform.position = horizontalPosition;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the final position is set
        transform.position = targetPosition;
        if (targetPosition != GameManager.instance.boardManager.purgatory.position)
        {
            GameManager.instance.boardManager.BounceBoard();
        }
        isMoving = false;
    }
    
    public void DisplaySymbol(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.Log("Sprite is null.");
        }
        symbolRenderer.gameObject.SetActive(displayFloorSymbol);
        symbolRenderer.sprite = sprite;
        badgeSpriteRenderer.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        MaterialPropertyBlock block = new();
        planeRenderer.GetPropertyBlock(block);
        planeRenderer.material = new Material(planeRenderer.material);
        planeRenderer.material.SetColor(BaseColorID, color);
        if (pawn.player == Player.RED)
        {
            badgeBackgroundRenderer.color = redColor;
        }
        else
        {
            badgeBackgroundRenderer.color = blueColor;
        }
        
        planeRenderer.SetPropertyBlock(block);
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
    
    Tween currentTween;
    public void OnHovered(bool inIsHovered)
    {
        if (!pawn.isAlive) return;
        isHovered = inIsHovered;
        SetMeshOutline(isHovered, "HoverOutline");
        if (pawn.player == GameManager.instance.boardManager.player)
        {
            currentTween = Tween.LocalPosition(model.transform, inIsHovered ? new Vector3(0, Globals.HOVEREDHEIGHT, 0) : Vector3.zero, 0.3f, Ease.OutCubic);
        }
        else
        {
            if (model.transform.localPosition != Vector3.zero)
            {
                currentTween = Tween.LocalPosition(model.transform, Vector3.zero, 0.1f, Ease.OutCubic);
            }
        }
    }

    public void SetSelect(bool inIsSelected)
    {
        isSelected = inIsSelected;
        SetMeshOutline(isSelected, "SelectOutline");
        pointerCollider.enabled = !inIsSelected;
    }

    public void OnHighlight(bool inIsHighlighted)
    {
        SetMeshOutline(inIsHighlighted, "Fill");
    }
}
