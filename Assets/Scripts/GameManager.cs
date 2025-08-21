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

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            throw new("MORE THAN ONE GAMEMANAGER IN SCENE");
        }
        Debug.Log("Welcome to warmancer!");
        Debug.developerConsoleVisible = true;
        SettingsManager.Initialize();
        StellarManager.Initialize();
        cameraManager.Initialize();
        guiMenuController.Initialize();
        audioManager.Initialize();
        Globals.InputActions.Game.Enable();
        Debug.Log("InputActions enabled");
    }
    
    // TODO: move this somewhere else
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
    
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
