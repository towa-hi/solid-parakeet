using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "Pawn", menuName = "Scriptable Objects/Pawn")]

[System.Serializable]
public class PawnDef : ScriptableObject
{
    public string pawnName;
    public int power;

    public Sprite icon;
    // graphics
    // sounds
}
