using UnityEngine;

public class GraveyardEntry : MonoBehaviour
{
    public Team team;
    public Rank rank;
    public GameObject model;
    public uint max;
    public uint alive;

    public PawnSprite pawnSprite;

    public void Initialize(Team team, Rank rank, uint max, uint alive)
    {
        this.team = team;
        this.rank = rank;
        this.max = max;
        this.alive = alive;
        // if (alive < max)
        // {
        //     model.SetActive(true);
        // }
        // else
        // {
        //     model.SetActive(false);
        // }
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(rank);
        if (team == Team.RED)
        {
            pawnSprite.SetSprite(pawnDef.redGraveyardSprite);
        }
        else
        {
            pawnSprite.SetSprite(pawnDef.blueGraveyardSprite);
        }
    }

    public void Refresh(uint alive)
    {
        this.alive = alive;
        // if (alive < max)
        // {
        //     model.SetActive(true);
        // }
        // else
        // {
        //     model.SetActive(false);
        // }
    }
}
