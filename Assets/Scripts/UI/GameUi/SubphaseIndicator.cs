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

    float redFlashUntil;
    float blueFlashUntil;
    static readonly float flashSpeed = 6f; // pulses per second
    static readonly Color redDim = Color.red * new Color(1, 1, 1, 0.5f);
    static readonly Color blueDim = Color.blue * new Color(1, 1, 1, 0.5f);

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
        redBg.color = redNeedsAction ? Color.red : redDim;
        blueBg.color = blueNeedsAction ? Color.blue : blueDim;
    }

    // Minimal flashing: call to make the check pulse briefly
    public void FlashRed(float duration)
    {
        redFlashUntil = Mathf.Max(redFlashUntil, Time.unscaledTime + duration);
    }

    public void FlashBlue(float duration)
    {
        blueFlashUntil = Mathf.Max(blueFlashUntil, Time.unscaledTime + duration);
    }

    void Update()
    {
        // Pulse alpha on checks while flash is active
        if (redCheck != null && Time.unscaledTime < redFlashUntil)
        {
            float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * flashSpeed) + 1f) * 0.5f; // 0..1
            Color c = redCheck.color;
            c.a = Mathf.Lerp(0.4f, 1f, t);
            redCheck.color = c;
        }
        if (blueCheck != null && Time.unscaledTime < blueFlashUntil)
        {
            float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * flashSpeed) + 1f) * 0.5f;
            Color c = blueCheck.color;
            c.a = Mathf.Lerp(0.4f, 1f, t);
            blueCheck.color = c;
        }
    }
}
