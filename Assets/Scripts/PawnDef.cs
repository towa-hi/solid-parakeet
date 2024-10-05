using UnityEngine;
[CreateAssetMenu(fileName = "Pawn", menuName = "Scriptable Objects/Pawn")]

[System.Serializable]
public class PawnDef : ScriptableObject
{
    public BoardManager boardManager;
    public string pawnName;
    public int power;
    
    // graphics
    // sounds
}
