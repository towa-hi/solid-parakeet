using System;
using System.Linq;
using UnityEngine;
using Contract;

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
        SnapshotPawnDelta? mRedDelta = null;
        SnapshotPawnDelta? mBlueDelta = null;
        if (battle.participants.Length is <= 0 or > 2)
        {
            throw new Exception("battle is malformed");
        }
        // Derive team and rank from pawn deltas
        foreach (PawnId pawnId in battle.participants)
        {
            SnapshotPawnDelta pawnDelta = delta.pawnDeltas[pawnId];
            Team team = pawnId.GetTeam();
            if (team == Team.RED)
            {
                mRedDelta = pawnDelta;
            }
            else if (team == Team.BLUE)
            {
                mBlueDelta = pawnDelta;
            }
        }
        if (mRedDelta is not SnapshotPawnDelta redDelta)
        {
            throw new Exception("battle lacks red pawn");
        }
        if (mBlueDelta is not SnapshotPawnDelta blueDelta)
        {
            throw new Exception("battle lacks blue pawn");
        }
        pawnL.Initialize(redDelta);
        pawnR.Initialize(blueDelta);
        arenaCamera.enabled = true;
    }
}
