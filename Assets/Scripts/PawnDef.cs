using System;
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

[Serializable]
public struct SPawnDef
{
    public string pawnName;
    public int power;

    public SPawnDef(SPawnDef copy)
    {
        pawnName = copy.pawnName;
        power = copy.power;
    }
    
    public SPawnDef(PawnDef pawnDef)
    {
        pawnName = pawnDef.pawnName;
        power = pawnDef.power;
    }

    public PawnDef ToUnity()
    {
        return Globals.GetPawnDefFromName(pawnName);
    }
}