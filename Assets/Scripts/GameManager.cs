using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Volume globalVolume;
    public BoardManager boardManager;
    public GuiManager guiManager;
    public CameraManager cameraManager;
    public AudioManager audioManager;
    public PoolManager poolManager;
    public SettingsManager settingsManager;
    public StellarManager stellarManager;
    public IGameClient client;
    public bool offlineMode;

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
            Resources.Load<PawnDef>("Pawn/99-unknown"),
        };
    }

    public Dictionary<Rank, PawnDef> GetPawnDefFromRank()
    {
        // NOTE: this is a nasty hack that wont work on the server side. we need to keep a lookup table for this later instead of loading from unity
        Dictionary<Rank, PawnDef> rankDictionary = new Dictionary<Rank, PawnDef>();
        foreach (Rank rank in Enum.GetValues(typeof(Rank)))
        {
            PawnDef pawnDef = orderedPawnDefList.FirstOrDefault(def => def.rank == rank);
            rankDictionary.Add(rank, pawnDef);
        }
        return rankDictionary;
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
                t = OutExpo(t);
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
    public static float InExpo(float t) => (float)Math.Pow(2, 10 * (t - 1));
    public static float OutExpo(float t) => 1 - InExpo(1 - t);
    
    void SetDefaultPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey("CHEATMODE"))
        {
            settingsManager.SetCheatMode(false);
        }
        if (!PlayerPrefs.HasKey("FASTMODE"))
        {
            settingsManager.SetFastMode(false);
        }
        if (!PlayerPrefs.HasKey("DISPLAYBADGE"))
        {
            settingsManager.SetDisplayBadge(true);
        }
        if (!PlayerPrefs.HasKey("ROTATECAMERA"))
        {
            settingsManager.SetRotateCamera(false);
        }
    }
    
    void Start()
    {
        guiManager.Initialize();
        cameraManager.Initialize();
        Debug.Log("Enable input action");
        Globals.InputActions.Game.Enable();
    }
    
    public void SetOfflineMode(bool inOfflineMode)
    {
        offlineMode = inOfflineMode;
        if (inOfflineMode)
        {
            client = new FakeClient();
            client.OnRegisterClientResponse += OnRegisterClientResponse;
            client.OnDisconnect += OnDisconnect;
            client.OnErrorResponse += OnErrorResponse;
            client.OnRegisterNicknameResponse += OnRegisterNicknameResponse;
            client.OnGameLobbyResponse += OnGameLobbyResponse;
            client.OnLeaveGameLobbyResponse += OnLeaveGameLobbyResponse;
            client.OnReadyLobbyResponse += OnReadyLobbyResponse;
            client.OnDemoStartedResponse += OnDemoStartedResponse;
            client.OnSetupSubmittedResponse += OnSetupSubmittedResponse;
            client.OnSetupFinishedResponse += OnSetupFinishedResponse;
            client.OnMoveResponse += OnMoveResponse;
            client.OnResolveResponse += OnResolveResponse;
            Debug.Log("GameManager: Initialized FakeClient for offline mode.");
            client.ConnectToServer();
        }
        else
        {
            stellarManager.ConnectToNetwork();
            //client = new GameClient();
            Debug.Log("GameManager: Initialized GameClient for online mode.");
        }
    }
    
    void OnRegisterClientResponse(Response<string> response)
    {
        guiManager.OnRegisterClientResponse(response);
        client.SendRegisterNickname(Globals.GetNickname());
    }
    
    void OnDisconnect(Response<string> response)
    {
        guiManager.OnDisconnect(response);
    }
    
    void OnErrorResponse(ResponseBase response)
    {
        Debug.LogError(response.message);
        guiManager.OnErrorResponse(response);
    }
    
    void OnRegisterNicknameResponse(Response<string> response)
    {
        guiManager.OnRegisterNicknameResponse(response);
    }
    
    void OnGameLobbyResponse(Response<SLobby> response)
    {
        guiManager.OnGameLobbyResponse(response);
    }
    
    void OnLeaveGameLobbyResponse(Response<string> response)
    {
        guiManager.OnLeaveGameLobbyResponse(response);
    }
    void OnReadyLobbyResponse(Response<SLobby> response)
    {
        guiManager.OnReadyLobbyResponse(response);
    }

    void OnDemoStartedResponse(Response<SLobbyParameters> response)
    {
        boardManager.OnDemoStartedResponse(response);
        guiManager.OnDemoStartedResponse(response);
    }
    
    void OnSetupSubmittedResponse(Response<bool> response)
    {
        boardManager.OnSetupSubmittedResponse(response);
    }
    
    void OnSetupFinishedResponse(Response<SGameState> response)
    {
        boardManager.OnSetupFinishedResponse(response);
    }

    void OnMoveResponse(Response<bool> response)
    {
        boardManager.OnMoveResponse(response);
    }
    
    void OnResolveResponse(Response<SResolveReceipt> response)
    {
        boardManager.OnResolveResponse(response);
    }
    
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
