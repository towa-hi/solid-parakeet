using System;
using Contract;
using UnityEngine;
using UnityEngine.UI;

public class SubphaseIndicator : MonoBehaviour
{
    public Image redBg;

    public Image blueBg;

    public Image redCheck;

    public Image blueCheck;

    public void Set(Subphase subphase, Team hostTeam)
    {
        bool redNeedsAction = subphase switch
        {
            Subphase.Host => hostTeam == Team.RED,
            Subphase.Guest => hostTeam != Team.RED,
            Subphase.Both => true,
            Subphase.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(subphase), subphase, null),
        };
        
        bool blueNeedsAction = subphase switch
        {
            Subphase.Host => hostTeam != Team.RED,
            Subphase.Guest => hostTeam == Team.RED,
            Subphase.Both => true,
            Subphase.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(subphase), subphase, null),
        };

        redCheck.enabled = !redNeedsAction;
        blueCheck.enabled = !blueNeedsAction;
        redBg.color = redNeedsAction ? Color.red : Color.red * new Color(1, 1, 1, 0.5f);
        blueBg.color = blueNeedsAction ? Color.blue : Color.blue * new Color(1, 1, 1, 0.5f);
    }
}
