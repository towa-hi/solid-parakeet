using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Pawn", menuName = "Scriptable Objects/Pawn")]

[System.Serializable]
public class PawnDef : ScriptableObject
{
    public int id;
    public string pawnName;
    public int power;
    public Rank Rank => (Rank)id;
    
    // graphics
    
    public Sprite icon;
    public Sprite redSprite;
    public Sprite blueSprite;
    // sounds
    
    public Rank GetRank()
    {
        return (Rank)id;
    }
}

[Serializable]
public struct SPawnDef
{
    public int id;
    public string pawnName;
    public int power;
    public readonly Rank Rank => (Rank)id;
    
    public SPawnDef(PawnDef pawnDef)
    {
        id = pawnDef.id;
        pawnName = pawnDef.pawnName;
        power = pawnDef.power;
    }

    
    public readonly PawnDef ToUnity()
    {
        int myId = id;
        return GameManager.instance.orderedPawnDefList.FirstOrDefault(pawnDef => pawnDef.id == myId);
    }
}