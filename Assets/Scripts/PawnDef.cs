using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Pawn", menuName = "Scriptable Objects/Pawn")]

[System.Serializable]
public class PawnDef : ScriptableObject
{
    public string pawnName;
    public int power;

    public Sprite icon;
    public Sprite baseSprite;
    // graphics
    // sounds
}

[Serializable]
public struct SPawnDef
{
    public string pawnName;
    public int power;
    
    public SPawnDef(PawnDef pawnDef)
    {
        pawnName = pawnDef.pawnName;
        power = pawnDef.power;
    }

    public SPawnDef(string inPawnName, int inPower)
    {
        pawnName = inPawnName;
        power = inPower;
    }
    
    public readonly PawnDef ToUnity()
    {
        string s = pawnName;
        foreach (var kvp in GameManager.instance.orderedPawnDefList.Where(kvp => kvp.Key.pawnName == s))
        {
            return kvp.Key;
        }
        throw new KeyNotFoundException();
    }
}