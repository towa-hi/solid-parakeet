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
    Renderer tileRenderer;
    public SpriteRenderer symbolRenderer;
    
    public bool isPassable;
    public Player setupPlayer;
    public int autoSetupZone;
    
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
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
        setupPlayer = Player.NONE;
        autoSetupZone = 0;
        UpdateView();
    }

    public void SetSetupPlayer(Player inSetupPlayer)
    {
        if (setupPlayer == Player.NONE)
        {
            autoSetupZone = 0;
        }
        setupPlayer = inSetupPlayer;
        UpdateView();
    }

    public void SetSetupZone(int inSetupZone)
    {
        if (setupPlayer == Player.NONE)
        {
            return;
        }
        autoSetupZone = inSetupZone;
        UpdateView();
    }

    void UpdateView()
    {
        Color color = Color.white;
        if (isPassable)
        {
            if (setupPlayer == Player.RED)
            {
                color = Color.red;
            }
            else if (setupPlayer == Player.BLUE)
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
        SetSetupPlayer(tile.setupPlayer);
        SetSetupZone(tile.autoSetupZone);
    }

    void SetColor(Color color)
    {
        MaterialPropertyBlock block = new();
        tileRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorID, color);
        tileRenderer.SetPropertyBlock(block);
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
                break;
        }
    }
}
