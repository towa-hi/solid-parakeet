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
    Animator disabledWinnerPawnViewAnimator;

    // Cached battle context for animation events
    BattleEvent? currentBattle;
    SnapshotPawnDelta? currentRedDelta;
    SnapshotPawnDelta? currentBlueDelta;
    Team? currentWinningTeam;
    bool currentBothDie;


    void Awake()
    {
        instance = this;
        // Cache the base arena attack controller so we can restore/fallback when needed
        baseArenaAttackAnimationController = animator != null ? animator.runtimeAnimatorController : null;
    }

    public void Initialize(bool inIsHex)
    {
        Debug.Log($"ArenaController: Initialize {inIsHex}");
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

    void ResetArenaPawns()
    {
        // Reset both pawns comprehensively to handle interrupted animations
        ResetArenaPawn(pawnL);
        ResetArenaPawn(pawnR);
    }
    
    void ResetArenaPawn(ArenaPawn pawn)
    {
        if (pawn == null) return;
        
        // Reset PawnView animations and shader properties
        if (pawn.pawnView != null)
        {
            pawn.pawnView.ResetAnimatedValues();
            pawn.pawnView.EnsureModelVisible();
        }
        
        // Ensure animator is enabled and reset to idle
        if (pawn.animator != null)
        {
            if (!pawn.animator.enabled)
            {
                pawn.animator.enabled = true;
            }
            // Rebind to ensure clean state
            pawn.animator.Rebind();
            pawn.animator.Update(0f);
        }
    }

    public void StartBattle(BattleEvent battle, TurnResolveDelta delta)
    {
        // Reset everything before starting a new battle to handle interrupted animations
        ResetArenaPawns();
        
        // Ensure any previously disabled PawnView animator is re-enabled before starting a new battle
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

        // Ensure shader state is clean at the start of each battle
        if (pawnL != null && pawnL.pawnView != null) pawnL.pawnView.ResetAnimatedValues();
        if (pawnR != null && pawnR.pawnView != null) pawnR.pawnView.ResetAnimatedValues();
        
        // Start idle animations for both pawns (they should be looping idle at battle start)
        if (pawnL != null && pawnL.pawnView != null) pawnL.pawnView.animator.Play("Idle", 0, 0f);
        if (pawnR != null && pawnR.pawnView != null) pawnR.pawnView.animator.Play("Idle", 0, 0f);

        // Determine winner (exactly one survivor means a winner). Otherwise it's a tie/bounce.
        Team? winningTeam = null;
        bool redAlive = redDelta.postAlive;
        bool blueAlive = blueDelta.postAlive;
        bool bothDie = !redAlive && !blueAlive;
        if (redAlive && !blueAlive)
        {
            winningTeam = Team.RED;
        }
        else if (blueAlive && !redAlive)
        {
            winningTeam = Team.BLUE;
        }

        // Cache battle context for animation events
        currentBattle = battle;
        currentRedDelta = redDelta;
        currentBlueDelta = blueDelta;
        currentWinningTeam = winningTeam;
        currentBothDie = bothDie;

        // Apply the arena attack animation override based on the winner
        RuntimeAnimatorController controllerToUse = baseArenaAttackAnimationController;
        Debug.Log($"ArenaController: bothDie: {bothDie} winningTeam: {winningTeam}");
        if (!bothDie && winningTeam.HasValue)
        {
            Team team = winningTeam.Value;
            // Disable the PawnView animator on the winner so it doesn't conflict with arena animation
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
        else if (bothDie)
        {
            Debug.Log($"ArenaController: both pawns die setting controller (tie). rank: {pawnL.pawnDelta.postRank}");
            PawnDef bothDef = ResourceRoot.GetPawnDefFromRank(pawnL.pawnDelta.postRank);
            // arbitrary choice of using red attack override controller for tie
            controllerToUse = bothDef.redAttackOverrideController;
        }
        animator.runtimeAnimatorController = controllerToUse;
        
        // Rebind resets all animator parameters and clip states, then re-enters Entry
        animator.Rebind();
        
        animator.SetBool("IsTie", bothDie);
        animator.SetBool("RedRevealing", pawnL.pawnDelta.preRevealed);
        animator.SetBool("BlueRevealing", pawnR.pawnDelta.preRevealed);
        
        animator.Update(0f);
        

        arenaCamera.enabled = true;
    }

    void BothDieStub(SnapshotPawnDelta redDelta, SnapshotPawnDelta blueDelta)
    {
        Debug.Log("ArenaController: both pawns die (stub). Implement mutual KO visuals.");
    }

    // Animation Event entry point from Arena timeline. Decides whom to hurt based on cached battle context.
    public void PlayHurtAnimation()
    {
        if (!currentRedDelta.HasValue || !currentBlueDelta.HasValue)
        {
            Debug.LogWarning("PlayHurtAnimation called without a cached battle context.");
            return;
        }
        bool redAlive = currentRedDelta.Value.postAlive;
        bool blueAlive = currentBlueDelta.Value.postAlive;
        if (!redAlive && !blueAlive)
        {
            if (pawnL != null && pawnL.pawnView != null)
            {
                pawnL.pawnView.HurtAnimation();
            }
            if (pawnR != null && pawnR.pawnView != null)
            {
                pawnR.pawnView.HurtAnimation();
            }
            return;
        }
        if (redAlive && !blueAlive)
        {
            if (pawnR != null && pawnR.pawnView != null)
            {
                pawnR.pawnView.HurtAnimation();
            }
            return;
        }
        if (blueAlive && !redAlive)
        {
            if (pawnL != null && pawnL.pawnView != null)
            {
                pawnL.pawnView.HurtAnimation();
            }
            return;
        }
        // Tie/bounce: no loser to hurt
    }
    public void FadeOutSprite()
    {
        if (!currentRedDelta.HasValue || !currentBlueDelta.HasValue)
        {
            Debug.LogWarning("FadeOutSprite called without a cached battle context.");
            return;
        }
        bool redAlive = currentRedDelta.Value.postAlive;
        bool blueAlive = currentBlueDelta.Value.postAlive;

        void FadePawn(ArenaPawn pawn)
        {
            if (pawn == null || pawn.pawnView == null) return;
            pawn.pawnView.FadeOut();
        }

        if (!redAlive && !blueAlive)
        {
            FadePawn(pawnL);
            FadePawn(pawnR);
            return;
        }
        if (redAlive && !blueAlive)
        {
            FadePawn(pawnR);
            return;
        }
        if (blueAlive && !redAlive)
        {
            FadePawn(pawnL);
            return;
        }
        // Tie/bounce: no loser to fade
    }
    // Animation Event: Shatter the loser's sprite using PawnSprite's Shatter reference
    public void ShatterLoserSprite()
    {
        if (!currentRedDelta.HasValue || !currentBlueDelta.HasValue)
        {
            Debug.LogWarning("ShatterLoserSprite called without a cached battle context.");
            return;
        }
        bool redAlive = currentRedDelta.Value.postAlive;
        bool blueAlive = currentBlueDelta.Value.postAlive;

        // Helper local function
        void ShatterPawn(ArenaPawn pawn)
        {
            if (pawn == null || pawn.pawnView == null) return;
            var pawnSprite = pawn.pawnView.GetComponentInChildren<PawnSprite>(true);
            if (pawnSprite != null && pawnSprite.shatter != null)
            {
                pawnSprite.shatter.ShatterEffect();
            }
        }

        if (!redAlive && !blueAlive)
        {
            ShatterPawn(pawnL);
            ShatterPawn(pawnR);
            return;
        }
        if (redAlive && !blueAlive)
        {
            ShatterPawn(pawnR);
            return;
        }
        if (blueAlive && !redAlive)
        {
            ShatterPawn(pawnL);
            return;
        }
        // Tie/bounce: no loser to shatter
    }
}
