using System;
using UnityEngine;

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

    public void Initialize(Tile inTile, bool isHex)
    {
        tile = inTile;
        gameObject.name = $"Tile ({tile.pos.x}, {tile.pos.y})";
        if (isHex)
        {
            hexTileModel.SetActive(true);
            squareTileModel.SetActive(false);
        }
        else
        {
            hexTileModel.SetActive(false);
            squareTileModel.SetActive(true);
        }
        tileModel = isHex ? hexTileModel : squareTileModel;
        tileModel.gameObject.SetActive(true);
        GameManager.instance.testBoardManager.OnPhaseChanged += OnPhaseChanged;
        ShowTile(tile.isPassable);
    }

    void OnPhaseChanged(ITestPhase phase)
    {
        switch (phase)
        {
            case SetupTestPhase setupTestPhase:
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
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));

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
