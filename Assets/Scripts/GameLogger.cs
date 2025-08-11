using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract;
using Stellar;
using UnityEngine;

public static class GameLogger
{
    const ulong ExpirationTicks = 172800000000000UL; // 2 days in ticks (matches CacheManager)

    static readonly Dictionary<string, History> KeyToHistory = new();

    public static void Initialize(GameNetworkState netState)
    {
        string key = GetHistoryKey(netState.address, netState.lobbyInfo.index);
        History? loaded = LoadHistory(key);
        History history = loaded ?? new History
        {
            game_state = netState.gameState,
            lobby_info = netState.lobbyInfo,
            lobby_parameters = netState.lobbyParameters,
            turns = Array.Empty<Turn>(),
        };

        // Ensure metadata reflects latest
        history.game_state = netState.gameState;
        history.lobby_info = netState.lobbyInfo;
        history.lobby_parameters = netState.lobbyParameters;

        KeyToHistory[key] = history;
        SaveHistory(key, history, netState.address);
    }

    public static void RecordNetworkState(GameNetworkState netState)
    {
        string key = GetHistoryKey(netState.address, netState.lobbyInfo.index);
        if (!KeyToHistory.TryGetValue(key, out History history))
        {
            Initialize(netState);
            history = KeyToHistory[key];
        }

        // Update header fields
        history.game_state = netState.gameState;
        history.lobby_info = netState.lobbyInfo;
        history.lobby_parameters = netState.lobbyParameters;

        // Build current turn entry from revealed proofs
        HiddenMove[] hostProofs = netState.gameState.moves?.ElementAtOrDefault(0).move_proofs ?? Array.Empty<HiddenMove>();
        HiddenMove[] guestProofs = netState.gameState.moves?.ElementAtOrDefault(1).move_proofs ?? Array.Empty<HiddenMove>();
        Turn currentTurn = new Turn
        {
            guest_move_proofs = guestProofs,
            host_move_proofs = hostProofs,
        };

        int turnIndex = (int)netState.gameState.turn;
        // Ensure list size
        List<Turn> turns = history.turns?.ToList() ?? new List<Turn>();
        while (turns.Count <= turnIndex)
        {
            turns.Add(new Turn
            {
                guest_move_proofs = Array.Empty<HiddenMove>(),
                host_move_proofs = Array.Empty<HiddenMove>(),
            });
        }
        // Merge only if we have new data; prefer non-empty proofs
        Turn previous = turns[turnIndex];
        bool hasNewGuest = (currentTurn.guest_move_proofs?.Length ?? 0) > 0;
        bool hasNewHost = (currentTurn.host_move_proofs?.Length ?? 0) > 0;
        turns[turnIndex] = new Turn
        {
            guest_move_proofs = hasNewGuest ? currentTurn.guest_move_proofs : previous.guest_move_proofs ?? Array.Empty<HiddenMove>(),
            host_move_proofs = hasNewHost ? currentTurn.host_move_proofs : previous.host_move_proofs ?? Array.Empty<HiddenMove>(),
        };
        history.turns = turns.ToArray();

        KeyToHistory[key] = history;
        SaveHistory(key, history, netState.address);
    }

    static string GetHistoryKey(AccountAddress address, LobbyId lobbyId)
    {
        return $"{address}_{lobbyId}_history";
    }

    static History? LoadHistory(string key)
    {
        string serialized = PlayerPrefs.GetString(key, null);
        if (serialized == null) return null;
        CacheContainer? container = DeserializeContainer(serialized);
        if (container == null) return null;
        if (IsExpired(container.Value.createdTimestamp)) return null;
        try
        {
            using MemoryStream dataStream = new(Convert.FromBase64String(container.Value.base64XdrData));
            SCVal sc = SCValXdr.Decode(new XdrReader(dataStream));
            return SCUtility.SCValToNative<History>(sc);
        }
        catch
        {
            return null;
        }
    }

    static void SaveHistory(string key, History history, AccountAddress address)
    {
        SCVal sc = SCUtility.NativeToSCVal(history);
        string base64XdrData = SCValXdr.EncodeToBase64(sc);
        CacheContainer container = new()
        {
            createdTimestamp = (ulong)DateTime.UtcNow.Ticks,
            base64XdrData = base64XdrData,
        };
        SCVal containerSc = SCUtility.NativeToSCVal(container);
        string serialized = SCValXdr.EncodeToBase64(containerSc);
        PlayerPrefs.SetString(key, serialized);
        PlayerPrefs.Save();
        AddCacheKeyForAddress(address, key);
    }

    static CacheContainer? DeserializeContainer(string serialized)
    {
        try
        {
            using MemoryStream memoryStream = new(Convert.FromBase64String(serialized));
            SCVal val = SCValXdr.Decode(new XdrReader(memoryStream));
            return SCUtility.SCValToNative<CacheContainer>(val);
        }
        catch
        {
            return null;
        }
    }

    static bool IsExpired(ulong timestamp)
    {
        ulong current = (ulong)DateTime.UtcNow.Ticks;
        return current - timestamp > ExpirationTicks;
    }

    static string GetMasterKeyListKey(AccountAddress address)
    {
        return $"cache_keys_{address}";
    }

    static string[] GetCacheKeysForAddress(AccountAddress address)
    {
        string masterKey = GetMasterKeyListKey(address);
        string serialized = PlayerPrefs.GetString(masterKey, null);
        if (serialized == null) return Array.Empty<string>();
        return serialized.Split(',');
    }

    static void AddCacheKeyForAddress(AccountAddress address, string cacheKey)
    {
        string[] existing = GetCacheKeysForAddress(address);
        if (existing.Contains(cacheKey)) return;
        List<string> list = existing.ToList();
        list.Add(cacheKey);
        string masterKey = GetMasterKeyListKey(address);
        PlayerPrefs.SetString(masterKey, string.Join(",", list.ToArray()));
        PlayerPrefs.Save();
    }

    [Serializable]
    struct CacheContainer : IScvMapCompatable
    {
        public ulong createdTimestamp;
        public string base64XdrData;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("createdTimestamp", createdTimestamp),
                    SCUtility.FieldToSCMapEntry("base64XdrData", base64XdrData),
                }),
            };
        }
    }
}
