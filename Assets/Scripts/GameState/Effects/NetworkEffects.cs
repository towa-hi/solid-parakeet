// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Contract;
// using UnityEngine;

// public sealed class NetworkEffects : IGameEffect
// {
//     GameStore store;

//     public void Initialize(GameStore s)
//     {
//         store = s;
//     }

//     public void OnActionAndEvents(GameAction action, IReadOnlyList<GameEvent> events, GameSnapshot state)
//     {
//         long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//         switch (action)
//         {
//             case NetworkStateChanged a:
//             {
//                 // // Polling is managed by UI mode (GuiGame). No action here.
//                 // // Auto-submit proofs when entering/progressing MoveProve or RankProve
//                 // if (StellarManager.IsBusy)
//                 // {
//                 //     break;
//                 // }
//                 // GameNetworkState net = state.Net;
//                 // // Auto ProveMove if snapshot can build req (phase/subphase validated inside)
//                 // if (state.TryBuildProveMoveReqFromCache(out var prove))
//                 // {
//                 //     // Inline ProveMove request
//                 //     store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = a, TimestampMs = now }));
//                 //     Result<bool> result = await StellarManager.ProveMoveRequest(prove, net.address, net.lobbyInfo, net.lobbyParameters);
//                 //     if (result.IsError)
//                 //     {
//                 //         HandleFatalNetworkError(result.Message);
//                 //         store.Dispatch(new UiWaitingForResponse(null));
//                 //         return;
//                 //     }
//                 //     await StellarManager.UpdateState();
//                 //     store.Dispatch(new UiWaitingForResponse(null));
//                 // }
//                 // // Auto ProveRank if snapshot can build req (phase/subphase validated inside)
//                 // if (state.TryBuildProveRankReqFromCache(out var req))
//                 // {
//                 //     // Inline ProveRank request
//                 //     store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = a, TimestampMs = now }));
//                 //     Result<bool> result = await StellarManager.ProveRankRequest(req);
//                 //     if (result.IsError)
//                 //     {
//                 //         HandleFatalNetworkError(result.Message);
//                 //         store.Dispatch(new UiWaitingForResponse(null));
//                 //         return;
//                 //     }
//                 //     await StellarManager.UpdateState();
//                 //     store.Dispatch(new UiWaitingForResponse(null));
//                 // }
//                 break;
//             }
//             case SetupSubmit:
//             {
//                 // GameNetworkState net = state.Net;
//                 // if (!state.TryBuildCommitSetupReq(out var req))
//                 // {
//                 //     Debug.LogWarning("[NetworkEffects] SetupSubmit: no setup commits to submit; skipping");
//                 //     break;
//                 // }
//                 // store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
//                 // Result<bool> submit = await StellarManager.CommitSetupRequest(req);
//                 // if (submit.IsError)
//                 // {
//                 //     HandleFatalNetworkError(submit.Message);
//                 //     store.Dispatch(new UiWaitingForResponse(null));
//                 //     return;
//                 // }
//                 // await StellarManager.UpdateState();
//                 // store.Dispatch(new UiWaitingForResponse(null));
//                 break;
//             }
//             case MoveSubmit:
//             {
//                 // GameNetworkState net = state.Net;
//                 // Debug.Log($"[NetworkEffects] MoveSubmit: enter mode={state.Mode} phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} busy={StellarManager.IsBusy}");
//                 // if (!net.IsMySubphase())
//                 // {
//                 //     Debug.Log($"[NetworkEffects] MoveSubmit: skipped isMySubphase={net.IsMySubphase()}");
//                 //     break;
//                 // }
//                 // if (!state.TryBuildCommitMoveAndProveMoveReqs(out var commit, out var prove))
//                 // {
//                 //     Debug.LogWarning("[NetworkEffects] MoveSubmit: could not build move reqs; skipping");
//                 //     break;
//                 // }
//                 // long tStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//                 // Debug.Log("[NetworkEffects] MoveSubmit: dispatch UiWaitingForResponse start");
//                 // store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
//                 // Debug.Log("[NetworkEffects] MoveSubmit: calling CommitMoveRequest");
//                 // Result<bool> result = await StellarManager.CommitMoveRequest(commit, prove, net.address, net.lobbyInfo, net.lobbyParameters);
//                 // if (result.IsError)
//                 // {
//                 //     Debug.LogError($"[NetworkEffects] MoveSubmit: CommitMoveRequest error: {result.Message}");
//                 //     HandleFatalNetworkError(result.Message);
//                 //     Debug.Log("[NetworkEffects] MoveSubmit: clearing UiWaitingForResponse (error)");
//                 //     store.Dispatch(new UiWaitingForResponse(null));
//                 //     return;
//                 // }
//                 // long tAfterCommit = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//                 // Debug.Log($"[NetworkEffects] MoveSubmit: CommitMoveRequest ok in {tAfterCommit - tStart}ms");
//                 // Debug.Log("[NetworkEffects] MoveSubmit: calling UpdateState");
//                 // await StellarManager.UpdateState();
//                 // long tAfterUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//                 // Debug.Log($"[NetworkEffects] MoveSubmit: UpdateState ok in {tAfterUpdate - tAfterCommit}ms (total {tAfterUpdate - tStart}ms)");
//                 // Debug.Log("[NetworkEffects] MoveSubmit: clearing UiWaitingForResponse (success)");
//                 // store.Dispatch(new UiWaitingForResponse(null));
//                 // Debug.Log("[NetworkEffects] MoveSubmit: exit");
//                 break;
//             }
//         }
//     }

// }


