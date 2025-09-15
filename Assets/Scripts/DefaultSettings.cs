using UnityEngine;

[CreateAssetMenu(fileName = "DefaultSettings", menuName = "Scriptable Objects/DefaultSettings")]
public class DefaultSettings : ScriptableObject
{
    public string defaultHostSneed;
    public string defaultGuestSneed;
    public string defaultContractAddress;
    public bool cheatMode;
    public bool fastMode;
    public bool displayBadges;
    public bool moveCamera;
    public int masterVolume;
    public int musicVolume;
    public int effectsVolume;
    public bool serializationLogging;
    public bool networkLogging;
    public bool pollingLogging;
    public bool stellarManagerLogging;
    public string defaultTestnetUri;
    public string defaultMainnetUri;
}
