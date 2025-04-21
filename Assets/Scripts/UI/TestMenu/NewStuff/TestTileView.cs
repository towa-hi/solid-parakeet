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
    void OnClientGameStateChanged(Lobby cachedLobby)
    {
        bool phaseChanged = cachedLobby.phase != oldPhase;
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(false);
                    tileModel.renderEffect.ClearEffects();
                }
                bool queued = false;
                if (movementTestPhase.queuedMove != null)
                {
                    if (movementTestPhase.queuedMove.pos == tile.pos)
                    {
                        queued = true;
                    }
                }
                tileModel.renderEffect.SetEffect(EffectType.FILL, queued);
                if (movementTestPhase.highlightedTiles.Contains(this))
                {
                    EnableEmission(true);
                    SetTopEmission(Color.green);
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
