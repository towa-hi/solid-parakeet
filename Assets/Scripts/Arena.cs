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
    public void Initialize(SPawn redPawn, SPawn bluePawn, bool inRedDies, bool inBlueDies, Action inOnFinish)
    {
        // TODO: figure out why the wrong pawn shatters when blue moves into stationary red 
        redDies = inRedDies;
        blueDies = inBlueDies;
        redPawnView.pawn = redPawn.ToUnity();
        redPawnView.SetColor(Color.red);
        redPawnView.DisplaySymbol(redPawnView.pawn.def.icon);
        bluePawnView.pawn = bluePawn.ToUnity();
        bluePawnView.SetColor(Color.blue);
        bluePawnView.DisplaySymbol(bluePawnView.pawn.def.icon);
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
