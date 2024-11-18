using System;
using UnityEngine;
using UnityEngine.U2D;

public class SetupPawnView : PawnView
{
    public override void Initialize(Pawn inPawn, TileView tileView)
    {
        base.Initialize(inPawn, tileView);
        //billboard.SetActive(false);
    }

}
