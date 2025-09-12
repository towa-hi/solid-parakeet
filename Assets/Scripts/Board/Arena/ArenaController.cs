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
    public Animator animator;
    RuntimeAnimatorController baseArenaAttackAnimationController;
    
    bool isHex;
    ArenaTiles tiles;


    void Awake()
    {
        instance = this;
        // Cache the base arena attack controller so we can restore/fallback when needed
        baseArenaAttackAnimationController = animator != null ? animator.runtimeAnimatorController : null;
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

        // Determine winner (exactly one survivor means a winner). Otherwise it's a tie/bounce.
        Team? winningTeam = null;
        bool redAlive = redDelta.postAlive;
        bool blueAlive = blueDelta.postAlive;
        if (redAlive && !blueAlive)
        {
            winningTeam = Team.RED;
        }
        else if (blueAlive && !redAlive)
        {
            winningTeam = Team.BLUE;
        }

        // Apply the arena attack animation override based on the winner
        RuntimeAnimatorController controllerToUse = baseArenaAttackAnimationController;
        if (winningTeam.HasValue)
        {
            Team team = winningTeam.Value;
            Rank winnerRank = team == Team.RED ? redDelta.postRank : blueDelta.postRank;
            PawnDef winnerDef = ResourceRoot.GetPawnDefFromRank(winnerRank);
            if (winnerDef != null)
            {
                AnimatorOverrideController attackOverride = team == Team.RED
                    ? winnerDef.redAttackOverrideController
                    : winnerDef.blueAttackOverrideController;
                if (attackOverride != null)
                {
                    controllerToUse = attackOverride;
                }
            }
        }

        if (animator != null)
        {
            animator.runtimeAnimatorController = controllerToUse;
            // Ensure the animation starts from the beginning
            animator.Rebind();
            animator.Update(0f);
            animator.Play("Combat", 0, 0f);
        }

        arenaCamera.enabled = true;
    }
}
