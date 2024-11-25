using System;
using System.Collections;
using UnityEngine;

public class Arena : MonoBehaviour
{
    public PawnView redPawnView;
    public PawnView bluePawnView;
    Action OnFinish;
    
    public void Initialize(Pawn redPawn, Pawn bluePawn, bool redDies, bool blueDies, Action inOnFinish)
    {
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
        yield return new WaitForSeconds(2f);
        OnFinish?.Invoke();
    }
}
