using System;
using System.Collections.Generic;
using UnityEngine;

public static class SettingsManager
{
    public static event Action<SettingsKey, int> OnSettingChanged;

    const string PrefKey = "WarmancerSettings";
    static WarmancerSettings _cache;
    static bool _isCacheLoaded;

    public static void Initialize()
    {
        if (PlayerPrefs.HasKey(PrefKey))
        {
            // Load into cache once
            string json = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                _cache = JsonUtility.FromJson<WarmancerSettings>(json);
                _isCacheLoaded = true;
                return;
            }
        }

        // Seed from DefaultSettings ScriptableObject
        DefaultSettings defaults = ResourceRoot.DefaultSettings;
        _cache = new WarmancerSettings
        {
            cheatMode = defaults.cheatMode,
            fastMode = defaults.fastMode,
            displayBadges = defaults.displayBadges,
            moveCamera = defaults.moveCamera,
            masterVolume = defaults.masterVolume,
            musicVolume = defaults.musicVolume,
            effectsVolume = defaults.effectsVolume,
        };
        _isCacheLoaded = true;
        string initialJson = JsonUtility.ToJson(_cache);
        PlayerPrefs.SetString(PrefKey, initialJson);
        PlayerPrefs.Save();
    }

    public static WarmancerSettings Load()
    {
        if (!_isCacheLoaded)
        {
            Initialize();
        }
        return _cache;
    }

    public static void Save(WarmancerSettings settings)
    {
        WarmancerSettings previous = _cache;
        _cache = settings;
        _isCacheLoaded = true;
        string json = JsonUtility.ToJson(_cache);
        PlayerPrefs.SetString(PrefKey, json);
        PlayerPrefs.Save();
        // Fire change events for keys that changed so systems can react (e.g., audio)
        if (previous.masterVolume != _cache.masterVolume)
        {
            OnSettingChanged?.Invoke(SettingsKey.MASTERVOLUME, _cache.masterVolume);
        }
        if (previous.musicVolume != _cache.musicVolume)
        {
            OnSettingChanged?.Invoke(SettingsKey.MUSICVOLUME, _cache.musicVolume);
        }
        if (previous.effectsVolume != _cache.effectsVolume)
        {
            OnSettingChanged?.Invoke(SettingsKey.EFFECTSVOLUME, _cache.effectsVolume);
        }
        if ((previous.cheatMode ? 1 : 0) != (_cache.cheatMode ? 1 : 0))
        {
            OnSettingChanged?.Invoke(SettingsKey.CHEATMODE, _cache.cheatMode ? 1 : 0);
        }
        if ((previous.fastMode ? 1 : 0) != (_cache.fastMode ? 1 : 0))
        {
            OnSettingChanged?.Invoke(SettingsKey.FASTMODE, _cache.fastMode ? 1 : 0);
        }
        if ((previous.displayBadges ? 1 : 0) != (_cache.displayBadges ? 1 : 0))
        {
            OnSettingChanged?.Invoke(SettingsKey.DISPLAYBADGES, _cache.displayBadges ? 1 : 0);
        }
        if ((previous.moveCamera ? 1 : 0) != (_cache.moveCamera ? 1 : 0))
        {
            OnSettingChanged?.Invoke(SettingsKey.MOVECAMERA, _cache.moveCamera ? 1 : 0);
        }
    }

    // Backward-compat wrappers (ModalSettings, legacy code). Prefer Load/Save going forward.
    public static int GetPref(SettingsKey key)
    {
        WarmancerSettings s = Load();
        switch (key)
        {
            case SettingsKey.CHEATMODE: return s.cheatMode ? 1 : 0;
            case SettingsKey.FASTMODE: return s.fastMode ? 1 : 0;
            case SettingsKey.DISPLAYBADGES: return s.displayBadges ? 1 : 0;
            case SettingsKey.MOVECAMERA: return s.moveCamera ? 1 : 0;
            case SettingsKey.MASTERVOLUME: return s.masterVolume;
            case SettingsKey.MUSICVOLUME: return s.musicVolume;
            case SettingsKey.EFFECTSVOLUME: return s.effectsVolume;
            default: return 0;
        }
    }

    public static void SetPrefs(WarmancerSettings settings)
    {
        Save(settings);
    }

    public static void SetPrefs(Dictionary<SettingsKey, int> changed)
    {
        if (changed == null || changed.Count == 0) return;
        WarmancerSettings s = Load();
        foreach (KeyValuePair<SettingsKey, int> kv in changed)
        {
            ApplyPref(ref s, kv.Key, kv.Value);
            OnSettingChanged?.Invoke(kv.Key, kv.Value);
        }
        Save(s);
    }

    static void SetPref(SettingsKey key, int val)
    {
        WarmancerSettings s = Load();
        ApplyPref(ref s, key, val);
        Save(s);
        OnSettingChanged?.Invoke(key, val);
    }

    static void ApplyPref(ref WarmancerSettings s, SettingsKey key, int val)
    {
        switch (key)
        {
            case SettingsKey.CHEATMODE: s.cheatMode = val == 1; break;
            case SettingsKey.FASTMODE: s.fastMode = val == 1; break;
            case SettingsKey.DISPLAYBADGES: s.displayBadges = val == 1; break;
            case SettingsKey.MOVECAMERA: s.moveCamera = val == 1; break;
            case SettingsKey.MASTERVOLUME: s.masterVolume = val; break;
            case SettingsKey.MUSICVOLUME: s.musicVolume = val; break;
            case SettingsKey.EFFECTSVOLUME: s.effectsVolume = val; break;
            default: break;
        }
    }
}

public enum SettingsKey
{
    CHEATMODE,
    FASTMODE,
    DISPLAYBADGES,
    MOVECAMERA,
    MASTERVOLUME,
    MUSICVOLUME,
    EFFECTSVOLUME,
}


public struct WarmancerSettings
{
    public bool cheatMode;
    public bool fastMode;
    public bool displayBadges;
    public bool moveCamera;
    public int masterVolume;
    public int musicVolume;
    public int effectsVolume;
}