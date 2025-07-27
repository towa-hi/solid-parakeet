using System;
using System.Collections.Generic;
using UnityEngine;

public static class SettingsManager
{
    public static event Action<SettingsKey, int> OnSettingChanged;
    
    static void SetPref(SettingsKey key, int val)
    {
        string stringKey = key.ToString();
        switch (key)
        {
            case SettingsKey.CHEATMODE:
            case SettingsKey.FASTMODE:
            case SettingsKey.DISPLAYBADGES:
            case SettingsKey.MOVECAMERA:
                if (val != 0 && val != 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(val));
                }
                break;
            case SettingsKey.MASTERVOLUME:
            case SettingsKey.MUSICVOLUME:
            case SettingsKey.EFFECTSVOLUME:
                if (val is < 0 or > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(val));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, null);
        }
        PlayerPrefs.SetInt(stringKey, val);
        Debug.Log($"Set {stringKey} to {val}");
        OnSettingChanged?.Invoke(key, val);
    }

    public static int GetPref(SettingsKey key)
    {
        return PlayerPrefs.GetInt(key.ToString());
    }

    public static void SetPrefs(Dictionary<SettingsKey, int> settings)
    {
        foreach (KeyValuePair<SettingsKey, int> setting in settings)
        {
            SetPref(setting.Key, setting.Value);
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