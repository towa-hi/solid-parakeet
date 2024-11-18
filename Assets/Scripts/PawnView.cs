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
    
    void OnDestroy()
    {
        
    }

    void Start()
    {
        
    }
    
    public virtual void Initialize(Pawn inPawn, TileView tileView)
    {
        pawn = inPawn;
        gameObject.name = $"Pawn {pawn.def.pawnName}";
        GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
        DisplaySymbol(Globals.pawnSprites[inPawn.def.pawnName]);
        switch (inPawn.player)
        {
            case Player.RED:
                SetCubeColor(Color.red);
                break;
            case Player.BLUE:
                SetCubeColor(Color.blue);
                break;
        }
        transform.position = tileView.pawnOrigin.position;
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
    
}
