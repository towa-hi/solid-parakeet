using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public class WalletManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void JSCheckWallet();

    [DllImport("__Internal")]
    static extern void JSGetFreighterAddress();

    [DllImport("__Internal")]
    static extern void JSGetNetworkDetails();

    public static string address;
    public static NetworkDetails networkDetails;
    
    public static bool webGL;
    
    public static WalletManager instance;
    
    
    // wrapper tasks
    static TaskCompletionSource<JSResponse> checkWalletTaskSource;
    static TaskCompletionSource<JSResponse> getAddressTaskSource;
    static TaskCompletionSource<JSResponse> getNetworkDetailsTaskSource;
    
    static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        #if UNITY_WEBGL
            webGL = true;
        #endif
    }

    public static async Task<bool> ConnectWallet()
    {
        bool walletExists = await CheckWallet();
        address = null;
        networkDetails = null;
        if (!walletExists)
        {
            Debug.LogWarning("Wallet could not be found");
            return false;
        }
        address = await GetAddress();
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogWarning("Address not found");
            return false;
        }
        string networkDetailsJson = await GetNetworkDetails();
        if (string.IsNullOrEmpty(networkDetailsJson))
        {
            Debug.LogWarning("Network details not found");
            return false;
        }
        try
        {
            // First try to parse as error
            var errorObj = JsonConvert.DeserializeAnonymousType(networkDetailsJson, new { error = "" }, jsonSettings);
            if (!string.IsNullOrEmpty(errorObj?.error))
            {
                Debug.LogError($"Network details error: {errorObj.error}");
                return false;
            }
            // If no error, parse as network details
            NetworkDetails networkDetailsObj = JsonConvert.DeserializeObject<NetworkDetails>(networkDetailsJson, jsonSettings);
            if (networkDetailsObj == null)
            {
                Debug.LogError("Invalid network details format");
                return false;
            }
            Debug.Log($"Connected to network: {networkDetailsObj.network}");
            networkDetails = networkDetailsObj;
            return true;
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Failed to parse network details: {ex.Message}");
            return false;
        }
    }
    
    static async Task<bool> CheckWallet()
    {
        if (checkWalletTaskSource != null && !checkWalletTaskSource.Task.IsCompleted)
        {
            throw new Exception("CheckWallet() is already in progress");
        }
        checkWalletTaskSource = new TaskCompletionSource<JSResponse>();
        JSCheckWallet();
        JSResponse checkWalletRes = await checkWalletTaskSource.Task;
        checkWalletTaskSource = null;
        if (checkWalletRes.code != 1)
        {
            Debug.Log("CheckWallet() failed with code " + checkWalletRes.code);
            return false;
        };
        Debug.Log("CheckWallet() completed");
        return true;
    }

    static async Task<string> GetAddress()
    {
        if (getAddressTaskSource != null && !getAddressTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddressFromFreighter() is already in progress");
        }
        getAddressTaskSource = new TaskCompletionSource<JSResponse>();
        JSGetFreighterAddress();
        JSResponse getAddressRes = await getAddressTaskSource.Task;
        getAddressTaskSource = null;
        if (getAddressRes.code != 1)
        {
            Debug.Log("GetAddress() failed with code " + getAddressRes.code);
            return null;
        }
        Debug.Log($"GetAddress() completed with data {getAddressRes.data}");
        return getAddressRes.data;
    }

    static async Task<string> GetNetworkDetails()
    {
        if (getNetworkDetailsTaskSource != null && !getNetworkDetailsTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetNetworkDetails() is already in progress");
        }
        getNetworkDetailsTaskSource = new TaskCompletionSource<JSResponse>();
        JSGetNetworkDetails();
        JSResponse getNetworkDetailsRes = await getNetworkDetailsTaskSource.Task;
        getNetworkDetailsTaskSource = null;
        if (getNetworkDetailsRes.code != 1)
        {
            Debug.Log("GetNetworkDetails() failed with code " + getNetworkDetailsRes.code);
            return null;
        }
        Debug.Log($"GetNetworkDetails() completed with data {getNetworkDetailsRes.data}");
        return getNetworkDetailsRes.data;
    }
    
    public void StellarResponse(string json)
    {
        try
        {
            JSResponse response = JsonUtility.FromJson<JSResponse>(json);
            if (response.code == -666)
            {
                throw new Exception($"StellarResponse() got unspecified error: {response}");
            }
            TaskCompletionSource<JSResponse> task = response.function switch
            {
                "_JSCheckWallet" => checkWalletTaskSource,
                "_JSGetFreighterAddress" => getAddressTaskSource,
                "_JSGetNetworkDetails" => getNetworkDetailsTaskSource,
                _ => throw new Exception($"StellarResponse() function not found {response}")
            };
            if (task == null)
            {
                throw new Exception($"StellarResponse() task was null: {response}");
            }
            task.SetResult(response);
        }
        catch (Exception e)
        {
            Debug.Log($"StellarResponse() unspecified error {e}");
            throw;
        }
    }
}

public class NetworkDetails
{
    public string network { get; set; }
    public string networkUrl { get; set; }
    public string networkPassphrase { get; set; }
    public string sorobanRpcUrl { get; set; }
}

[Serializable]
public class JSResponse
{
    public string function;
    public int code;
    public string data;
}