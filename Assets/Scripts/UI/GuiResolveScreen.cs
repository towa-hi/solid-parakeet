using System;
using UnityEngine;

public class GuiResolveScreen : MonoBehaviour
{
    public Arena arena;

    public void Initialize(SPawn redPawn, SPawn bluePawn, bool redDies, bool blueDies, Action onFinish)
    {
        gameObject.SetActive(true);
        arena.Initialize(redPawn, bluePawn, redDies, blueDies, onFinish);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
