using System;
using UnityEngine;
using UnityEngine.Serialization;

public class BoardMakerTile : MonoBehaviour
{
    public Vector2Int pos;

    public GameObject squareTile;
    public Renderer squareTileRenderer;
    public GameObject hexTile;
    public Renderer hexTileRenderer;

    GameObject tileModel;
    public Renderer tileRenderer;
    public SpriteRenderer symbolRenderer;
    
    public bool isPassable;
    public Team setupTeam;
    public int autoSetupZone;
    
    static readonly int BaseColorID = Shader.PropertyToID("_Color");
    static readonly int MainTexID = Shader.PropertyToID("_BaseMap");

    public void Initialize(Vector2Int inPos, bool isHex)
    {
        pos = inPos;
        if (isHex)
        {
            tileModel = hexTile;
            squareTile.SetActive(false);
            hexTile.SetActive(true);
            tileRenderer = hexTileRenderer;
        }
        else
        {
            tileModel = squareTile;
            squareTile.SetActive(true);
            hexTile.SetActive(false);
            tileRenderer = squareTileRenderer;
        }
        
    }

    
    public void SetIsPassable(bool inIsPassable)
    {
        isPassable = inIsPassable;
        setupTeam = Team.NONE;
        UpdateView();
    }

    public void SetSetupTeam(Team inSetupTeam)
    {
        setupTeam = inSetupTeam;
        UpdateView();
    }

    public void SetSetupZone(int inSetupZone)
    {
        autoSetupZone = inSetupZone;
        UpdateView();
    }

    void UpdateView()
    {
        Color color = Color.white;
        if (isPassable)
        {
            if (setupTeam == Team.RED)
            {
                color = Color.red;
            }
            else if (setupTeam == Team.BLUE)
            {
                color = Color.blue;
            }
        }
        else
        {
            color = Color.gray;
        }
        SetColor(color);
        SetSymbol();
    }
    
    public void LoadState(Tile tile)
    {
        SetIsPassable(tile.isPassable);
        SetSetupTeam(tile.setupTeam);
        SetSetupZone(tile.autoSetupZone);
    }

    void SetColor(Color color)
    {
        Debug.Log($"SetColor: {color}");
        tileRenderer.material.SetColor(BaseColorID, color);
    }
    
    public Sprite zone0;
    public Sprite zone1;
    public Sprite zone2;
    public Sprite zone3;
    
    void SetSymbol()
    {
        switch (autoSetupZone)
        {
            case 0:
                symbolRenderer.sprite = zone0;
                break;
            case 1:
                symbolRenderer.sprite = zone1;
                break;
            case 2:
                symbolRenderer.sprite = zone2;
                break;
            case 3:
                symbolRenderer.sprite = zone3;
                break;
            default:
                throw new Exception("invalid zone set");
        }
    }
}
