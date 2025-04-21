using System;
using System.Linq;
using Contract;
using UnityEngine;

public class Badge : MonoBehaviour
{
    public SpriteRenderer symbolRenderer;
    public SpriteRenderer backgroundRenderer;

    public void Initialize(Contract.Pawn p, bool active)
    {
        //PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
        //symbolRenderer.sprite = def.icon;
        switch ((Team)p.team)
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

    public void InitializeSetup(PawnCommitment p, Team team, bool active)
    {
        PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
        symbolRenderer.sprite = def.icon;
        switch (team)
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
