using System;
using System.Collections;
using UnityEngine;

public class Arena : MonoBehaviour
{
    public GuiResolveScreen master;
    public PawnView redPawnView;
    public PawnView bluePawnView;
    Action OnFinish;
    public bool redDies;
    public bool blueDies;
    public Camera arenaCamera;
    
    public void Initialize(GuiResolveScreen inMaster, SPawn redPawn, SPawn bluePawn, bool inRedDies, bool inBlueDies, Action inOnFinish)
    {
        master = inMaster;
        redDies = inRedDies;
        blueDies = inBlueDies;
        redPawnView.pawn = redPawn.ToUnity();
        redPawnView.SetColor(Color.red);
        redPawnView.DisplaySymbol(redPawnView.pawn.def.icon);
        bluePawnView.pawn = bluePawn.ToUnity();
        bluePawnView.SetColor(Color.blue);
        bluePawnView.DisplaySymbol(bluePawnView.pawn.def.icon);
        OnFinish = inOnFinish;
        arenaCamera.gameObject.SetActive(true);
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
        arenaCamera.gameObject.SetActive(false);
        master.HideBattleScreen();
        OnFinish?.Invoke();
    }
}
