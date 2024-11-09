using UnityEngine;
using UnityEngine.U2D;

public class PawnView : MonoBehaviour
{
    public GameObject model;
    public GameObject cube;
    public GameObject plane;

    public SpriteAtlas symbols;
    public SpriteRenderer symbolRenderer;
    
    public Pawn pawn;

    
    public void Initialize(Pawn inPawn, TileView tileView)
    {
        pawn = inPawn;
        gameObject.name = $"Pawn {pawn.def.pawnName}";
        GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
        DisplaySymbol(Globals.pawnSprites[inPawn.def.pawnName]);
        transform.position = tileView.pawnOrigin.position;
    }

    public void DisplaySymbol(string index)
    {
        
        Sprite sprite = symbols.GetSprite(index);
        //Debug.Log(symbols.spriteCount);
        if (sprite == null)
        {
            Debug.Log("wew");
        }
        symbolRenderer.sprite = sprite;
    }
    
}
