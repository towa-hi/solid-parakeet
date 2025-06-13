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
    static Dictionary<string, ProveSetupReq> proveSetupReqCache = new Dictionary<string, ProveSetupReq>();
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

    public static HiddenRank LoadHiddenRank(byte[] hash)
    {
        if (hash.All(b => b == 0))
        {
            throw new ArgumentException("Hash can't be 0");
        }
        string key = Convert.ToBase64String(hash);
        if (hiddenRankCache.TryGetValue(key, out HiddenRank rank))
        {
            return rank;
        }
        return GetFromPlayerPrefs<HiddenRank>(key);
    }

    public static string SaveProveSetupReq(ProveSetupReq proveSetupReq)
    {
        if (proveSetupReq.salt == 0)
        {
            throw new ArgumentException("Salt can't be 0");
        }
        string key = SaveToPlayerPrefs(proveSetupReq);
        proveSetupReqCache[key] = proveSetupReq;
        return key;
    }

    public static ProveSetupReq LoadProveSetupReq(byte[] hash)
    {
        if (hash.All(b => b == 0))
        {
            throw new ArgumentException("Hash can't be 0");
        }
        string key = Convert.ToBase64String(hash);
        if (proveSetupReqCache.TryGetValue(key, out ProveSetupReq rank))
        {
            return rank;
        }
        return GetFromPlayerPrefs<ProveSetupReq>(key);
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
}
