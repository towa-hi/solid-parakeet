using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Pawn", menuName = "Scriptable Objects/Pawn")]

[System.Serializable]
public class PawnDef : ScriptableObject
{
    public int id;
    public Rank rank;
    public string pawnName;
    public int power;
    public int movementRange;
    
    // graphics
    
    public Sprite icon;
    public Sprite redSprite;
    public Sprite blueSprite;

    public AnimatorOverrideController redAnimatorOverrideController;
    public AnimatorOverrideController blueAnimatorOverrideController;
    public AnimatorOverrideController redAttackOverrideController;
    public AnimatorOverrideController blueAttackOverrideController;
    public GameObject redCard;
    public GameObject blueCard;

    public Sprite redGraveyardSprite;
    public Sprite blueGraveyardSprite;
    
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
    public int rank;
    public string pawnName;
    public int power;
    public int movementRange;
    
    public readonly Rank Rank => (Rank)id;
    
    public SPawnDef(PawnDef pawnDef)
    {
        id = pawnDef.id;
        rank = (int)pawnDef.rank;
        pawnName = pawnDef.pawnName;
        power = pawnDef.power;
        movementRange = pawnDef.movementRange;
    }

    
    public readonly PawnDef ToUnity()
    {
        int myId = id;
        return ResourceRoot.OrderedPawnDefs.FirstOrDefault(pawnDef => pawnDef.id == myId);
    }
}