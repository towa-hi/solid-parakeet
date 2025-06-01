using System;
using System.Linq;
using Contract;
using UnityEngine;

public class Badge : MonoBehaviour
{
    public SpriteRenderer symbolRenderer;
    public SpriteRenderer backgroundRenderer;
    public Color redBackgroundColor;
    public Color blueBackgroundColor;
    public void Initialize(Contract.Pawn p, bool active)
    {
        //PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
        //symbolRenderer.sprite = def.icon;
        switch ((Team)p.team)
        {
            case Team.RED:
                backgroundRenderer.color = redBackgroundColor;
                break;
            case Team.BLUE:
                backgroundRenderer.color = blueBackgroundColor;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (active && !string.IsNullOrEmpty(p.pawn_def_hash))
        {
            PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
            symbolRenderer.sprite = def.icon;
        }
        
        gameObject.SetActive(active);
    }

    public void Initialize(PawnCommitment p, Team team, bool active)
    {
        PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
        symbolRenderer.sprite = def.icon;
        switch (team)
        {
            case Team.RED:
                backgroundRenderer.color = redBackgroundColor;
                break;
            case Team.BLUE:
                backgroundRenderer.color = blueBackgroundColor;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        gameObject.SetActive(active);
    }
}
