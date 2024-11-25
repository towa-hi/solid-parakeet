using System;
using UnityEngine;

public class GuiResolveScreen : MonoBehaviour
{
    public Arena arena;

    public void Initialize(PawnView redPawn, PawnView bluePawn, bool redDies, bool blueDies, Action onFinish)
    {
        gameObject.SetActive(true);
        arena.Initialize(redPawn.pawn, bluePawn.pawn, redDies, blueDies, onFinish);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
