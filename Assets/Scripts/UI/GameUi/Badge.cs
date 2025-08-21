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

    public void SetBadge(Team team, Rank? rank)
    {
        PawnDef def = ResourceRoot.GetPawnDefFromRank(rank);
        symbolRenderer.sprite = def.icon;
        backgroundRenderer.color = team switch
        {
            Team.RED => redBackgroundColor,
            Team.BLUE => blueBackgroundColor,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
