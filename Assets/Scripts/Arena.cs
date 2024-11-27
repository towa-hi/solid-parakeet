using System;
using System.Collections;
using UnityEngine;

public class Arena : MonoBehaviour
{
    public PawnView redPawnView;
    public PawnView bluePawnView;
    Action OnFinish;
    public bool redDies;
    public bool blueDies;
    public void Initialize(Pawn redPawn, Pawn bluePawn, bool inRedDies, bool inBlueDies, Action inOnFinish)
    {
        // TODO: figure out why the wrong pawn shatters when blue moves into stationary red 
        redDies = inRedDies;
        blueDies = inBlueDies;
        redPawnView.pawn = redPawn;
        redPawnView.SetColor(Color.red);
        redPawnView.DisplaySymbol(redPawn.def.icon);
        bluePawnView.pawn = bluePawn;
        bluePawnView.SetColor(Color.blue);
        bluePawnView.DisplaySymbol(bluePawn.def.icon);
        OnFinish = inOnFinish;
        StartCoroutine(Battle());
    }

    IEnumerator Battle()
    {
        if (redDies)
        {
            redPawnView.shatterEffect.ShatterEffect(20f);
        }

        if (blueDies)
        {
            bluePawnView.shatterEffect.ShatterEffect(20f);
        }
        yield return new WaitForSeconds(2f);
        OnFinish?.Invoke();
    }
}
