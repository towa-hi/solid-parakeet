using System;
using Contract;

public abstract record GameEvent;

// Setup visuals events (store-driven)
public record SetupRankSelectedEvent(Rank? OldRank, Rank? NewRank) : GameEvent;
public record SetupPendingChangedEvent(System.Collections.Generic.Dictionary<PawnId, Rank?> OldMap, System.Collections.Generic.Dictionary<PawnId, Rank?> NewMap) : GameEvent;


