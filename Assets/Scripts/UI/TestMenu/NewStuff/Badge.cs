using System;
using UnityEngine;

public class Badge : MonoBehaviour
{
    public SpriteRenderer symbolRenderer;
    public SpriteRenderer backgroundRenderer;

    public void Initialize(Pawn pawn, bool active)
    {
        symbolRenderer.sprite = pawn.def.icon;
        switch (pawn.team)
        {
            case Team.NONE:
                break;
            case Team.RED:
                backgroundRenderer.color = Color.red;
                break;
            case Team.BLUE:
                backgroundRenderer.color = Color.blue;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        gameObject.SetActive(active);
    }
}
