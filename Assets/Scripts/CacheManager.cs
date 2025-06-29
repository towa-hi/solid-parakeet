using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Contract;
using Stellar;
using UnityEngine;

public static class CacheManager
{
    static Dictionary<string, HiddenRank> hiddenRankCache = new Dictionary<string, HiddenRank>();
    static Dictionary<string, Setup> setupCache = new Dictionary<string, Setup>();
    static Dictionary<string, HiddenMove> hiddenMoveCache = new Dictionary<string, HiddenMove>();
    static Dictionary<uint, long> lobbyExpirationCache = new Dictionary<uint, long>();
    public static byte[] SaveHiddenRank(HiddenRank hiddenRank)
    {
        if (hiddenRank.salt == 0)
        {
            throw new ArgumentException("Salt can't be 0");
        }
        string key = SaveToPlayerPrefs(hiddenRank);
        hiddenRankCache[key] = hiddenRank;
        return Convert.FromBase64String(key);
    }

    public static HiddenRank? LoadHiddenRank(byte[] hash)
    {
        if (hash.Length != 16)
        {
            throw new ArgumentException("Invalid hidden rank hash length");
        }
        if (hash.All(b => b == 0))
        {
            return null;
        }
        string key = Convert.ToBase64String(hash);
        if (hiddenRankCache.TryGetValue(key, out HiddenRank rank))
        {
            return rank;
        }
        if (PlayerPrefs.HasKey(key))
        {
            return GetFromPlayerPrefs<HiddenRank>(key);
        }
        else
        {
            return null;
        }
        
    }

    public static string SaveSetupReq(Setup setup)
    {
        if (setup.salt == 0)
        {
            throw new ArgumentException("Salt can't be 0");
        }
        string key = SaveToPlayerPrefs(setup);
        setupCache[key] = setup;
        return key;
    }

    public static Setup LoadSetupReq(byte[] hash)
    {
        if (hash.All(b => b == 0))
        {
            throw new ArgumentException("Hash can't be 0");
        }
        string key = Convert.ToBase64String(hash);
        if (setupCache.TryGetValue(key, out Setup setup))
        {
            return setup;
        }
        return GetFromPlayerPrefs<Setup>(key);
    }

    static string SaveToPlayerPrefs(IScvMapCompatable obj)
    {
        SCVal scVal = SCUtility.NativeToSCVal(obj);
        string xdrString = SCValXdr.EncodeToBase64(scVal);
        byte[] hash = SCUtility.GetHash(scVal);
        string key = Convert.ToBase64String(hash);
        PlayerPrefs.SetString(key, xdrString);
        return key;
    }

    static T GetFromPlayerPrefs<T>(string key)
    {
        string xdrString = PlayerPrefs.GetString(key, null);
        if (xdrString == null)
        {
            throw new IndexOutOfRangeException();
        }
        using MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(xdrString));
        SCVal val = SCValXdr.Decode(new XdrReader(memoryStream));
        return SCUtility.SCValToNative<T>(val);
    }

    public static byte[] SaveMoveReq(HiddenMove hiddenMove)
    {
        if (hiddenMove.salt == 0)
        {
            throw new ArgumentException("Salt can't be 0");
        }
        string key = SaveToPlayerPrefs(hiddenMove);
        hiddenMoveCache[key] = hiddenMove;
        return Convert.FromBase64String(key);
    }

    public static HiddenMove LoadMoveReq(byte[] hash)
    {
        if (hash.All(b => b == 0))
        {
            throw new ArgumentException("Hash can't be 0");
        }
        string key = Convert.ToBase64String(hash);
        if (hiddenMoveCache.TryGetValue(key, out HiddenMove move))
        {
            return move;
        }
        return GetFromPlayerPrefs<HiddenMove>(key);
    }

    public static void CacheLobbyExpiration(uint lobbyId, long liveUntilLedgerSeq)
    {
        lobbyExpirationCache[lobbyId] = liveUntilLedgerSeq;
        PlayerPrefs.SetString($"lobby_expiry_{lobbyId}", liveUntilLedgerSeq.ToString());
    }

    public static long? GetLobbyExpiration(uint lobbyId)
    {
        if (lobbyExpirationCache.TryGetValue(lobbyId, out long cachedExpiry))
        {
            return cachedExpiry;
        }
        
        string storedExpiry = PlayerPrefs.GetString($"lobby_expiry_{lobbyId}", null);
        if (storedExpiry != null && long.TryParse(storedExpiry, out long parsedExpiry))
        {
            lobbyExpirationCache[lobbyId] = parsedExpiry;
            return parsedExpiry;
        }
        
        return null;
    }

    public static bool IsLobbyExpired(uint lobbyId, long currentLedgerSeq)
    {
        long? expiry = GetLobbyExpiration(lobbyId);
        return expiry.HasValue && currentLedgerSeq >= expiry.Value;
    }
}
