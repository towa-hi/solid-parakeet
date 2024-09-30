using UnityEngine;

[System.Serializable]
public class Pawn
{
    [SerializeField] public PawnDef def;
    public Player player;
    public Vector2Int pos;
    
    public Pawn(PawnDef inDef, Vector2Int inPos)
    {
        def = inDef;
        pos = inPos;
    }
}
