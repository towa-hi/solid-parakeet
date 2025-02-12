using System;
using System.Collections;
using PrimeTween;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.U2D;

public class PawnView : MonoBehaviour
{
    public GameObject model;
    public GameObject cube;
    public GameObject plane;
    public GameObject billboard;
    
    public bool isSetup;
    
    public SpriteAtlas symbols;
    public SpriteRenderer symbolRenderer;
    public ParentConstraint parentConstraint;
    
    public Pawn pawn;

    bool displayFloorSymbol;
    
    public MeshRenderer billboardRenderer;
    public MeshRenderer planeRenderer;
    public Shatter shatterEffect;
    bool isSelected;
    bool isHovered;
    bool isHighlighted;
    bool isMoving;
    
    public GameObject badge;
    public SpriteRenderer badgeSpriteRenderer;
    public SpriteRenderer badgeBackgroundRenderer;
    public Color redColor;
    public Color blueColor;
    
    public Collider pointerCollider;
    // Reference to the current movement coroutine
    Coroutine moveCoroutine;
    
    ConstraintSource parentSource;
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    
    public void Initialize(Pawn inPawn, TileView tileView)
    {
        parentSource = new ConstraintSource
        {
            sourceTransform = GameManager.instance.boardManager.purgatory,
            weight = 1,
        };
        parentConstraint.AddSource(parentSource);
        pawn = inPawn;
        string objectName = $"{pawn.team} SetupPawn {pawn.def.pawnName}";
        if (!isSetup)
        {
            objectName += $"{Shared.ShortGuid(pawn.pawnId)}";
        }
        gameObject.name = objectName;
        UpdateSprite();
        DisplaySymbol(pawn.def.icon);
        switch (inPawn.team)
        {
            case Team.RED:
                SetColor(redColor);
                break;
            case Team.BLUE:
                SetColor(blueColor);
                break;
        }
        if (tileView == null)
        {
            LockMovementToTransform(GameManager.instance.boardManager.purgatory);
        }
        else
        {
            LockMovementToTransform(tileView.pawnOrigin);
        }
        badge.SetActive(PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
    }

    
    
    public void LockMovementToTransform(Transform anchor)
    {
        SetParentConstraint(anchor);
        SetParentConstraintActive(true);
    }
    
    void SetParentConstraint(Transform parent)
    {
        parentSource = new ConstraintSource
        {
            sourceTransform = parent,
            weight = 1,
        };
        parentConstraint.SetSource(0, parentSource);
    }

    void SetParentConstraintActive(bool inActive)
    {
        if (isMoving)
        {
            throw new Exception("SetParentConstraintActive cannot be set when isMoving is happening");
        }
        parentConstraint.constraintActive = inActive;
    }
    
    public PawnChanges SyncState(SPawn state)
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
        return pawnChanges;
    }
    
    public void RevealPawn(SPawn sPawn)
    {
        Debug.Log($"REVEALPAWN {gameObject.name}");
        pawn.def = sPawn.def.ToUnity();
        gameObject.name = $"{pawn.team} Pawn {pawn.def.pawnName} {Shared.ShortGuid(pawn.pawnId)}";
        DisplaySymbol(pawn.def.icon);
        UpdateSprite();
    }

    void UpdateSprite()
    {
        Sprite displaySprite;
        if (pawn.team == Team.RED)
        {
            displaySprite = pawn.def.redSprite;
        }
        else
        {
            displaySprite = pawn.def.blueSprite;
        }
        
        billboard.GetComponent<SpriteToMesh>().Activate(displaySprite);
    }
    
    public IEnumerator ArcToPosition(Transform target, float duration, float arcHeight)
    {
        SetParentConstraintActive(false);
        isMoving = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // Calculate the normalized time (0 to 1)
            float t = elapsedTime / duration;
            t = Shared.EaseOutQuad(t);
            
            // Interpolate position horizontally
            Vector3 horizontalPosition = Vector3.Lerp(startPosition, target.position, t);

            // Calculate vertical arc using a parabolic equation
            float verticalOffset = arcHeight * (1 - Mathf.Pow(2 * t - 1, 2)); // Parabolic equation: a(1 - (2t - 1)^2)
            horizontalPosition.y += verticalOffset;

            // Apply the calculated position
            transform.position = horizontalPosition;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the final position is set
        isMoving = false;
        LockMovementToTransform(target);
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
        if (pawn.team == Team.RED)
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
    }

    public void OnSelect(bool inIsSelected)
    {
        isSelected = inIsSelected;
        SetMeshOutline(isSelected, "SelectOutline");
        pointerCollider.enabled = !inIsSelected;
    }

    public void OnHighlight(bool inIsHighlighted)
    {
        isHighlighted = inIsHighlighted;
        SetMeshOutline(inIsHighlighted, "Fill");
    }
}
