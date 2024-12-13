using UnityEngine;

public class SettingsManager : MonoBehaviour
{

    public void SetCheatMode(bool cheat)
    {
        PlayerPrefs.SetInt("CHEATMODE", cheat ? 1 : 0);
    }

    public void SetFastMode(bool fast)
    {
        PlayerPrefs.SetInt("FASTMODE", fast ? 1 : 0);
    }

    public void SetDisplayBadge(bool displayBadge)
    {
        PlayerPrefs.SetInt("DISPLAYBADGE", displayBadge ? 1 : 0);
    }

    public void SetRotateCamera(bool rotate)
    {
        PlayerPrefs.SetInt("ROTATECAMERA", rotate ? 1 : 0);
    }
}
