using System;
using System.Linq;
using UnityEngine;

public class ArenaController : MonoBehaviour
{
    public static ArenaController instance;
    public ArenaTiles squareTiles;

    public ArenaTiles hexTiles;
    public ArenaPawn pawnL;
    public ArenaPawn pawnR;

    public Camera arenaCamera;
    
    bool isHex;
    ArenaTiles tiles;


    void Awake()
    {
        instance = this;
    }

    public void Initialize(bool inIsHex)
    {
        isHex = inIsHex;
        squareTiles.SetShow(false);
        hexTiles.SetShow(false);
        tiles = isHex ? hexTiles : squareTiles;
        tiles.SetShow(true);
        arenaCamera.enabled = false;
    }

    public void Close()
    {
        arenaCamera.enabled = false;
        
    }

    public void StartBattle(BattleEvent battle, TurnResolveDelta delta)
    {
        // tile A is left, tile B is right
        // for now, red is always left and blue is always right
        SnapshotPawn? redPawn = null;
        SnapshotPawn? bluePawn = null;
        if (battle.participants.Length is <= 0 or > 2)
        {
            throw new Exception("battle is malformed");
        }
        foreach (SnapshotPawn thing in delta.post.pawns)
        {
            if (battle.participants.Contains(thing.pawnId))
            {
                if (thing.pawnId.GetTeam() == Team.RED)
                {
                    redPawn = thing;
                }
                else if (thing.pawnId.GetTeam() == Team.BLUE)
                {
                    bluePawn = thing;
                }
            }
        }

        if (redPawn == null)
        {
            throw new Exception("battle lacks red pawn");
        }

        if (bluePawn == null)
        {
            throw new Exception("battle lacks blue pawn");
        }
        
        pawnL.Initialize(redPawn.Value);
        pawnR.Initialize(bluePawn.Value);
        arenaCamera.enabled = true;
    }
}
