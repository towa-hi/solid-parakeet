using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public enum OnlineMode
{
    Online = 0,
    Offline = 1,
}

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

    // Online/Offline mode state
    public OnlineMode onlineMode = OnlineMode.Online;
    const string OnlineModePrefKey = "OnlineMode";


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
        // Initialize mode from PlayerPrefs before subsystems read it
        if (PlayerPrefs.HasKey(OnlineModePrefKey))
        {
            onlineMode = (OnlineMode)PlayerPrefs.GetInt(OnlineModePrefKey);
        }
        else
        {
            PlayerPrefs.SetInt(OnlineModePrefKey, (int)onlineMode);
        }
        cameraManager?.Initialize();
        guiMenuController?.Initialize();
        audioManager?.Initialize();
        Globals.InputActions.Game.Enable();
        Debug.Log("InputActions enabled");
    }
    
    
    public bool IsOnline()
    {
        return onlineMode == OnlineMode.Online;
    }

    public async Task<Result<bool>> ConnectToNetwork(ModalConnectData data)
    {
        if (data.isWallet)
        {
            Result<WalletManager.WalletConnection> walletResult = await WalletManager.ConnectWallet();
            if (walletResult.IsError)
            {
                return Result<bool>.Err(walletResult);
            }
        }
        var init = StellarManager.Initialize(data);
        if (init.IsError)
        {
            return Result<bool>.Err(init);
        }
        onlineMode = OnlineMode.Online;
        PlayerPrefs.SetInt(OnlineModePrefKey, (int)onlineMode);
        Debug.Log($"Switched to {onlineMode} mode");
        Result<bool> updateResult = await StellarManager.UpdateState();
        if (updateResult.IsError)
        {
            Debug.LogWarning("Network unavailable; switching to Offline mode.");
            await SwitchToOfflineInternal();
            return Result<bool>.Err(updateResult);
        }
        return Result<bool>.Ok(true);
    }

    public async Task OfflineMode()
    {
        await SwitchToOfflineInternal();

    }
    async Task SwitchToOfflineInternal()
    {
        // Abort any in-flight network task to prevent stuck UI states
        StellarManager.AbortCurrentTask();
        onlineMode = OnlineMode.Offline;
        PlayerPrefs.SetInt(OnlineModePrefKey, (int)onlineMode);
        await StellarManager.UpdateState();
        Debug.Log($"Switched to {onlineMode} mode");
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
