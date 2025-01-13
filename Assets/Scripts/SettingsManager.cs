using UnityEngine;

public class SettingsManager : MonoBehaviour
{

    public void SetCheatMode(bool cheat)
    {
        Debug.Log($"Set CHEATMODE to {cheat}");
        PlayerPrefs.SetInt("CHEATMODE", cheat ? 1 : 0);
    }

    public void SetFastMode(bool fast)
    {
        Debug.Log($"Set FASTMODE to {fast}");
        PlayerPrefs.SetInt("FASTMODE", fast ? 1 : 0);
    }

    public void SetDisplayBadge(bool displayBadge)
    {
        Debug.Log($"Set DISPLAYBADGE to {displayBadge}");
        PlayerPrefs.SetInt("DISPLAYBADGE", displayBadge ? 1 : 0);
    }

    public void SetRotateCamera(bool rotate)
    {
        Debug.Log($"Set ROTATECAMERA to {rotate}");
        PlayerPrefs.SetInt("ROTATECAMERA", rotate ? 1 : 0);
    }
}
