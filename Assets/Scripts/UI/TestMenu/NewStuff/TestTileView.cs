using System;
using Contract;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestTileView : MonoBehaviour
{
    public Transform origin;
    
    public TileModel tileModel;
    public TileModel hexTileModel;
    public TileModel squareTileModel;
    public Tile tile;
    public Color baseColor;
    public Color redTeamColor;
    public Color blueTeamColor;
    
    
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    TestBoardManager bm;
    
    public void Initialize(Tile inTile, TestBoardManager inBoardManager)
    {
        tile = inTile;
        bm = inBoardManager;
        //bm.clickInputManager.OnPositionHovered += OnHover;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        bm.clickInputManager.OnPositionHovered += OnHover;
        gameObject.name = $"Tile ({tile.pos.x}, {tile.pos.y})";
        hexTileModel.gameObject.SetActive(false);
        squareTileModel.gameObject.SetActive(false);
        tileModel = bm.boardDef.isHex ? hexTileModel : squareTileModel;
        tileModel.gameObject.SetActive(true);
        ShowTile(tile.isPassable);
    }

    void OnHover(Vector2Int pos)
    {
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, tile.pos == pos);
                break;
            case SetupTestPhase setupTestPhase:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    uint oldPhase = 999;
    void OnClientGameStateChanged(Lobby lobby, ITestPhase phase)
    {
        bool phaseChanged = lobby.phase != oldPhase;
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(false);
                }
                tileModel.renderEffect.ClearEffects();
                tileModel.renderEffect.SetEffect(EffectType.FILL, false);
                EnableEmission(false);
                if (movementTestPhase.clientState.queuedMove != null)
                {
                    bool isTarget = movementTestPhase.clientState.queuedMove.pos == tile.pos;
                    tileModel.renderEffect.SetEffect(EffectType.FILL, isTarget);
                    Contract.Pawn p = lobby.GetPawnById(movementTestPhase.clientState.queuedMove.pawnId);
                    bool isOrigin = p.pos.ToVector2Int() == tile.pos;
                    if (isOrigin)
                    {
                        SetTopEmission(Color.red);
                        EnableEmission(true);
                    }
                }
                else
                {
                    if (movementTestPhase.clientState.selectedPawnView)
                    {
                        Contract.Pawn p = lobby.GetPawnById(movementTestPhase.clientState.selectedPawnView.pawnId);
                        bool isOrigin = p.pos.ToVector2Int() == tile.pos;
                        if (isOrigin)
                        {
                            SetTopEmission(Color.red);
                            EnableEmission(true);
                        }
                        bool isHighlighted = movementTestPhase.clientState.highlightedTiles.Contains(this);
                        if (isHighlighted)
                        {
                            SetTopEmission(Color.green);
                            EnableEmission(true);
                        }
                    }
                }
                
                break;
            case SetupTestPhase setupTestPhase:
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(true);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        oldPhase = lobby.phase;
    }

    void SetSetupEmissionHighlight(bool highlight)
    {
        if (highlight)
        {
            switch (tile.setupTeam)
            {
                case Team.NONE:
                    EnableEmission(false);
                    SetTopEmission(baseColor);
                    break;
                case Team.RED:
                    EnableEmission(true);
                    SetTopEmission(redTeamColor);
                    break;
                case Team.BLUE:
                    EnableEmission(true);
                    SetTopEmission(blueTeamColor);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            EnableEmission(false);
            SetTopEmission(baseColor);
        }
    }
    
    void ShowTile(bool show)
    {
        tileModel.gameObject.SetActive(show);
    }
    
    void EnableEmission(bool emission)
    {
        Material mat = tileModel.topRenderer.material;
        if (emission)
        {
            mat.EnableKeyword("_EMISSION");
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
    }
    
    void SetTopEmission(Color color)
    {
        Material mat = tileModel.topRenderer.material;
        mat.SetColor(EmissionColor, color);
    }
    
    
}
