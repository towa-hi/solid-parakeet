using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Transform purgatory;
    
    public Volume globalVolume;
    [FormerlySerializedAs("testBoardManager")] public BoardManager boardManager;
    //public GuiManager guiManager;
    [FormerlySerializedAs("guiTestMenuController")] public GuiMenuController guiMenuController;
    public CameraManager cameraManager;
    public AudioManager audioManager;
    public PoolManager poolManager;

    public List<PawnDef> orderedPawnDefList;

    void Awake()
    {
        Debug.developerConsoleVisible = true;
        SetDefaultPlayerPrefs();
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
        orderedPawnDefList = new List<PawnDef>
        {
            Resources.Load<PawnDef>("Pawn/00-throne"),
            Resources.Load<PawnDef>("Pawn/01-assassin"),
            Resources.Load<PawnDef>("Pawn/02-scout"),
            Resources.Load<PawnDef>("Pawn/03-seer"),
            Resources.Load<PawnDef>("Pawn/04-grunt"),
            Resources.Load<PawnDef>("Pawn/05-knight"),
            Resources.Load<PawnDef>("Pawn/06-wraith"),
            Resources.Load<PawnDef>("Pawn/07-reaver"),
            Resources.Load<PawnDef>("Pawn/08-herald"),
            Resources.Load<PawnDef>("Pawn/09-champion"),
            Resources.Load<PawnDef>("Pawn/10-warlord"),
            Resources.Load<PawnDef>("Pawn/11-trap"),
            Resources.Load<PawnDef>("Pawn/12-unknown"),
        };
    }
    
    void Start()
    {
        cameraManager?.Initialize();
        guiMenuController?.Initialize();
        Debug.Log("Enable input action");
        Globals.InputActions.Game.Enable();
    }

    public PawnDef GetPawnDefFromRankTemp(Rank? mRank)
    {
        Rank rankTemp = Rank.UNKNOWN;
        if (mRank is Rank rank)
        {
            rankTemp = rank;
        }
        return orderedPawnDefList.FirstOrDefault(def => def.rank == rankTemp);
    }
    
    Coroutine lightningCoroutine;
    public void Lightning()
    {
        if (lightningCoroutine != null)
        {
            StopCoroutine(lightningCoroutine);
        }
        lightningCoroutine = StartCoroutine(LightningEffect());
    }

    IEnumerator LightningEffect()
    {
        if (globalVolume && globalVolume.profile.TryGet<ColorAdjustments>(out var colorAdjust))
        {

            colorAdjust.active = true;
            
            globalVolume.profile.TryGet<WhiteBalance>(out var whiteBalance);
            whiteBalance.active = true;
            
            // Set initial "flash" values
            float startExposure = 2f;
            float startContrast = 50f;
            float startSaturation = -100f;
            float startTemperature = -100f;
            colorAdjust.postExposure.value = startExposure;
            colorAdjust.contrast.value = startContrast;
            colorAdjust.saturation.value = startSaturation;
            whiteBalance.temperature.value = startTemperature;
            float fadeDuration = 2.5f;
            float elapsed = 0f;

            // Fade parameters back to 0 over half a second
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                t = Shared.OutExpo(t);
                colorAdjust.postExposure.value = Mathf.Lerp(startExposure, 0f, t);
                colorAdjust.contrast.value = Mathf.Lerp(startContrast, 0f, t);
                colorAdjust.saturation.value = Mathf.Lerp(startSaturation, 0f, t);
                whiteBalance.temperature.value = Mathf.Lerp(startTemperature, 0f, t);
                yield return null;
            }

            // Ensure reset
            colorAdjust.postExposure.value = 0f;
            colorAdjust.contrast.value = 0f;
            colorAdjust.saturation.value = 0f;
            whiteBalance.temperature.value = 0f;
            colorAdjust.active = false;
            whiteBalance.active = false;
        }
    }
    
    void SetDefaultPlayerPrefs()
    {
        Dictionary<SettingsKey, int> defaultSettings = new()
        {
            { SettingsKey.CHEATMODE, 0 },
            { SettingsKey.FASTMODE, 1 },
            { SettingsKey.DISPLAYBADGES, 1 },
            { SettingsKey.MOVECAMERA, 1 },
            { SettingsKey.MASTERVOLUME, 50 },
            { SettingsKey.MUSICVOLUME, 50 },
            { SettingsKey.EFFECTSVOLUME, 50 },
        };
        SettingsManager.SetPrefs(defaultSettings);
    }
    
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
