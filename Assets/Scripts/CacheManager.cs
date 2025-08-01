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
    
    static Dictionary<PawnId, CachedRankProof> hiddenRanksAndMerkleProofs = new();
    static Dictionary<string, HiddenMove> hiddenMoveCache = new();
    const ulong expirationTicks = 172800000000000UL; // 2 days in ticks
    
    public static void Initialize(AccountAddress address, LobbyId lobbyId)
    {
        hiddenRanksAndMerkleProofs.Clear();
        hiddenMoveCache.Clear();
        
        // Cleanup expired caches for this address
        CleanupExpiredCaches(address);
        // Load CachedRankProofs
        string rankProofKey = GetRankProofKey(address, lobbyId);
        
        CachedRankProof[] rankProofArray = LoadFromPlayerPrefs<CachedRankProof>(rankProofKey);
        foreach (CachedRankProof rankProof in rankProofArray)
        {
            PawnId key = rankProof.hidden_rank.pawn_id;
            hiddenRanksAndMerkleProofs.Add(key, rankProof);
        }
        
        // Load HiddenMoves
        string hiddenMovesKey = GetHiddenMovesKey(address, lobbyId);
        HiddenMove[] hiddenMoveArray = LoadFromPlayerPrefs<HiddenMove>(hiddenMovesKey);
        foreach (HiddenMove hiddenMove in hiddenMoveArray)
        {
            string key = Convert.ToBase64String(SCUtility.Get16ByteHash(hiddenMove));
            hiddenMoveCache.Add(key, hiddenMove);
        }
        HumanDebugNetworkInspector.UpdateCache(hiddenRanksAndMerkleProofs, hiddenMoveCache);
    }
    
    // public static void StoreHiddenRank(HiddenRank hiddenRank, AccountAddress address, LobbyId lobbyId)
    // {
    //     string hiddenRanksKey = GetHiddenRanksKey(address, lobbyId);
    //     HiddenRank[] existingArray = LoadFromPlayerPrefs<HiddenRank>(hiddenRanksKey);
    //     
    //     List<HiddenRank> hiddenRankList = existingArray.ToList();
    //     hiddenRankList.Add(hiddenRank);
    //     
    //     HiddenRank[] hiddenRankArray = hiddenRankList.ToArray();
    //     SaveToPlayerPrefs(hiddenRankArray, hiddenRanksKey, address);
    //     
    //     byte[] hash = SCUtility.Get16ByteHash(hiddenRank);
    //     string key = Convert.ToBase64String(hash);
    //     hiddenRankCache[key] = hiddenRank;
    // }

    
    public static void StoreHiddenRanksAndProofs(List<CachedRankProof> ranksAndProofs, AccountAddress address, LobbyId lobbyId)
    {
        string rankProofsKey = GetRankProofKey(address, lobbyId);
        SaveToPlayerPrefs(ranksAndProofs.ToArray(), rankProofsKey, address);
        foreach (CachedRankProof rankProof in ranksAndProofs)
        {
            hiddenRanksAndMerkleProofs.Add(rankProof.hidden_rank.pawn_id, rankProof);
        }
        HumanDebugNetworkInspector.UpdateCache(hiddenRanksAndMerkleProofs, hiddenMoveCache);
    }

    public static CachedRankProof? GetHiddenRankAndProof(PawnId pawnId)
    {
        if (hiddenRanksAndMerkleProofs.TryGetValue(pawnId, out CachedRankProof rankProof))
        {
            return rankProof;
        }
        return null;
    }
    
    public static HiddenMove? GetHiddenMove(byte[] hash)
    {
        if (hash.Length != 16)
        {
            throw new ArgumentException("Invalid hidden move hash length");
        }
        if (hash.All(b => b == 0))
        {
            return null;
        }
        string key = Convert.ToBase64String(hash);
        if (hiddenMoveCache.TryGetValue(key, out HiddenMove move))
        {
            return move;
        }
        else
        {
            return null;
        }
    }

    public static bool RankProofsCacheExists(AccountAddress address, LobbyId lobbyId)
    {
        string rankProofsKey = GetRankProofKey(address, lobbyId);
        return PlayerPrefs.HasKey(rankProofsKey);
    }

    public static bool HiddenMoveCacheExists(AccountAddress address, LobbyId lobbyId)
    {
        string hiddenMovesKey = GetHiddenMovesKey(address, lobbyId);
        return PlayerPrefs.HasKey(hiddenMovesKey);
    }
    
    public static void StoreHiddenMove(HiddenMove hiddenMove, AccountAddress address, LobbyId lobbyId)
    {
        string hiddenMovesKey = GetHiddenMovesKey(address, lobbyId);
        HiddenMove[] existingArray = LoadFromPlayerPrefs<HiddenMove>(hiddenMovesKey);
        
        List<HiddenMove> hiddenMoveList = existingArray.ToList();
        hiddenMoveList.Add(hiddenMove);
        
        HiddenMove[] hiddenMoveArray = hiddenMoveList.ToArray();
        SaveToPlayerPrefs(hiddenMoveArray, hiddenMovesKey, address);
        
        byte[] hash = SCUtility.Get16ByteHash(hiddenMove);
        string key = Convert.ToBase64String(hash);
        hiddenMoveCache[key] = hiddenMove;
        HumanDebugNetworkInspector.UpdateCache(hiddenRanksAndMerkleProofs, hiddenMoveCache);
    }
    
    public static void StoreHiddenMoves(HiddenMove[] hiddenMoves, AccountAddress address, LobbyId lobbyId)
    {
        string hiddenMovesKey = GetHiddenMovesKey(address, lobbyId);
        HiddenMove[] existingArray = LoadFromPlayerPrefs<HiddenMove>(hiddenMovesKey);
        
        List<HiddenMove> hiddenMoveList = existingArray.ToList();
        hiddenMoveList.AddRange(hiddenMoves);
        
        HiddenMove[] hiddenMoveArray = hiddenMoveList.ToArray();
        SaveToPlayerPrefs(hiddenMoveArray, hiddenMovesKey, address);
        
        foreach (HiddenMove hiddenMove in hiddenMoves)
        {
            byte[] hash = SCUtility.Get16ByteHash(hiddenMove);
            string key = Convert.ToBase64String(hash);
            hiddenMoveCache[key] = hiddenMove;
        }
        HumanDebugNetworkInspector.UpdateCache(hiddenRanksAndMerkleProofs, hiddenMoveCache);
    }


    static string GetRankProofKey(AccountAddress address, LobbyId lobbyId)
    {
        return $"{address}_{lobbyId}_rank_proofs";
    }
    
    static string GetHiddenMovesKey(AccountAddress address, LobbyId lobbyId)
    {
        return $"{address}_{lobbyId}_moves";
    }
    
    static string GetMasterKeyListKey(AccountAddress address)
    {
        return $"cache_keys_{address}";
    }
    
    static string[] GetCacheKeysForAddress(AccountAddress address)
    {
        string masterKey = GetMasterKeyListKey(address);
        string serialized = PlayerPrefs.GetString(masterKey, null);
        if (serialized == null)
        {
            return Array.Empty<string>();
        }
        return serialized.Split(',');
    }
    
    static void AddCacheKeyForAddress(AccountAddress address, string cacheKey)
    {
        string[] existingKeys = GetCacheKeysForAddress(address);
        if (!existingKeys.Contains(cacheKey))
        {
            List<string> keyList = existingKeys.ToList();
            keyList.Add(cacheKey);
            string masterKey = GetMasterKeyListKey(address);
            PlayerPrefs.SetString(masterKey, string.Join(",", keyList.ToArray()));
            PlayerPrefs.Save();
        }
    }
    
    static void RemoveCacheKeyForAddress(AccountAddress address, string cacheKey)
    {
        string[] existingKeys = GetCacheKeysForAddress(address);
        List<string> keyList = existingKeys.ToList();
        if (keyList.Remove(cacheKey))
        {
            string masterKey = GetMasterKeyListKey(address);
            if (keyList.Count == 0)
            {
                PlayerPrefs.DeleteKey(masterKey);
            }
            else
            {
                PlayerPrefs.SetString(masterKey, string.Join(",", keyList.ToArray()));
            }
            PlayerPrefs.Save();
        }
    }
    
    static CacheContainer? DeserializeContainer(string serialized)
    {
        if (serialized == null) return null;
        
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
        ulong currentTime = (ulong)DateTime.UtcNow.Ticks;
        return currentTime - timestamp > expirationTicks;
    }
    
    static T[] LoadFromPlayerPrefs<T>(string key)
    {
        string serialized = PlayerPrefs.GetString(key, null);
        CacheContainer? container = DeserializeContainer(serialized);
        
        if (container == null || IsExpired(container.Value.createdTimestamp))
        {
            return Array.Empty<T>();
        }
        
        try
        {
            using MemoryStream dataStream = new(Convert.FromBase64String(container.Value.base64XdrData));
            SCVal dataVal = SCValXdr.Decode(new XdrReader(dataStream));
            return SCUtility.SCValToNative<T[]>(dataVal);
        }
        catch
        {
            return Array.Empty<T>();
        }
    }
    
    static void SaveToPlayerPrefs<T>(T[] array, string key, AccountAddress address)
    {
        SCVal dataScVal = SCUtility.NativeToSCVal(array);
        string base64XdrData = SCValXdr.EncodeToBase64(dataScVal);
        
        CacheContainer container = new()
        {
            createdTimestamp = (ulong)DateTime.UtcNow.Ticks,
            base64XdrData = base64XdrData,
        };
        
        SCVal containerScVal = SCUtility.NativeToSCVal(container);
        string serialized = SCValXdr.EncodeToBase64(containerScVal);
        PlayerPrefs.SetString(key, serialized);
        PlayerPrefs.Save();
        Debug.Log($"Saved key {key} val {serialized}");
        AddCacheKeyForAddress(address, key);
    }
    
    static void CleanupExpiredCaches(AccountAddress address)
    {
        string[] cacheKeys = GetCacheKeysForAddress(address);
        
        foreach (string cacheKey in cacheKeys)
        {
            string serialized = PlayerPrefs.GetString(cacheKey, null);
            CacheContainer? container = DeserializeContainer(serialized);
            
            if (container == null || IsExpired(container.Value.createdTimestamp))
            {
                PlayerPrefs.DeleteKey(cacheKey);
                RemoveCacheKeyForAddress(address, cacheKey);
            }
        }
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
