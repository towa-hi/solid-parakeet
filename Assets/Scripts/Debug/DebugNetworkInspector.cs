using System;
using Contract;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;


public class DebugNetworkInspector : MonoBehaviour
{
    
    [Header("Client State")] 
    [SerializeField] public bool isRunning;
    [SerializeField] public bool isInitialized;
    [SerializeField] public string address;
    [SerializeField] public string stringDebug;

    [SerializeField] public string networkContext;
    
    public HumanDebugNetworkInspector humanDebugNetworkInspector;
    
    public static DebugNetworkInspector instance;
    
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        isRunning = true;
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    void OnNetworkStateUpdated()
    {
        isInitialized = true;
        UpdateDebugNetworkInspector();
        humanDebugNetworkInspector.UpdateDebugNetworkInspector(StellarManager.networkState);
    }

    public static void UpdateDebugNetworkInspector()
    {
        if (instance == null) return;
        
        // Convert networkState to JSON and store in stringDebug
        instance.stringDebug = JsonConvert.SerializeObject(StellarManager.networkState, Formatting.Indented);
        instance.networkContext = JsonConvert.SerializeObject(StellarManager.networkContext, Formatting.Indented);
    }
}
