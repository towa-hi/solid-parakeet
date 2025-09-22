using System;
using Contract;

public abstract record GameAction;

public record NetworkStateChanged(GameNetworkState Net, NetworkDelta Delta) : GameAction;


