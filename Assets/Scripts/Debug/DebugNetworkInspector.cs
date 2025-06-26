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
        UpdateDebugNetworkInspector(StellarManager.networkState);
        humanDebugNetworkInspector.UpdateDebugNetworkInspector(StellarManager.networkState);
    }

    public static void UpdateDebugNetworkInspector(NetworkState networkState)
    {
        if (instance == null) return;
        
        instance.address = networkState.address.ToString();
        
        // Convert networkState to JSON and store in stringDebug
        instance.stringDebug = JsonConvert.SerializeObject(networkState, Formatting.Indented);
    }
}
