using UnityEngine;

public class PawnPlacement
{
    
}

public class PawnPlacementBatch
{
    public Player owner;
    public PawnPlacement[] pawnPlacements;

    public PawnPlacementBatch(Player inOwner, PawnPlacement[] inPawnPlacements)
    {
        owner = inOwner;
        pawnPlacements = inPawnPlacements;
    }
}
