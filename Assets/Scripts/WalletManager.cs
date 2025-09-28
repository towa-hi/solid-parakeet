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
    
    [DllImport("__Internal")]
    static extern void JSSignTransaction(string unsignedTransactionEnvelope, string networkPassphrase);
    public static string address;
    public static NetworkDetails networkDetails;
    
    public static bool webGL;
    
    public static WalletManager instance;

    // Indicates a wallet modal/operation is active (e.g., connect, sign)
    public static bool IsWalletBusy { get; private set; }
    public static event Action<bool> OnWalletBusyChanged;

    static void SetWalletBusy(bool busy)
    {
        if (IsWalletBusy == busy) return;
        IsWalletBusy = busy;
        try
        {
            OnWalletBusyChanged?.Invoke(IsWalletBusy);
        }
        catch (Exception e)
        {
            Debug.LogError($"OnWalletBusyChanged handler threw: {e}");
        }
    }
    
    // wrapper tasks
    static TaskCompletionSource<JSResponse> checkWalletTaskSource;
    static TaskCompletionSource<JSResponse> getAddressTaskSource;
    static TaskCompletionSource<JSResponse> getNetworkDetailsTaskSource;
    static TaskCompletionSource<JSResponse> signTransactionTaskSource;
    
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

    public struct WalletConnection
    {
        public string address;
        public NetworkDetails networkDetails;
    }

    public static async Task<Result<WalletConnection>> ConnectWallet()
    {
        SetWalletBusy(true);
        Result<bool> check = await CheckWallet();
        address = null;
        networkDetails = null;
        if (check.IsError)
        {
            Debug.LogWarning("Wallet could not be found");
            SetWalletBusy(false);
            return Result<WalletConnection>.Err(check);
        }
        Result<string> addrRes = await GetAddress();
        if (addrRes.IsError)
        {
            Debug.LogWarning("Address not found");
            SetWalletBusy(false);
            return Result<WalletConnection>.Err(addrRes);
        }
        address = addrRes.Value;
        Result<string> ndRes = await GetNetworkDetails();
        if (ndRes.IsError)
        {
            Debug.LogWarning("Network details not found");
            SetWalletBusy(false);
            return Result<WalletConnection>.Err(ndRes);
        }
        string networkDetailsJson = ndRes.Value;
        try
        {
            // First try to parse as error
            var errorObj = JsonConvert.DeserializeAnonymousType(networkDetailsJson, new { error = "" }, jsonSettings);
            if (!string.IsNullOrEmpty(errorObj?.error))
            {
                Debug.LogError($"Network details error: {errorObj.error}");
                return Result<WalletConnection>.Err(StatusCode.WALLET_NETWORK_DETAILS_ERROR, errorObj.error);
            }
            // If no error, parse as network details
            NetworkDetails networkDetailsObj = JsonConvert.DeserializeObject<NetworkDetails>(networkDetailsJson, jsonSettings);
            if (networkDetailsObj == null)
            {
                Debug.LogError("Invalid network details format");
                return Result<WalletConnection>.Err(StatusCode.WALLET_PARSING_ERROR, "Invalid wallet network details format");
            }
            Debug.Log($"Connected to network: {networkDetailsObj.network}");
            networkDetails = networkDetailsObj;
            var ok = Result<WalletConnection>.Ok(new WalletConnection { address = address, networkDetails = networkDetailsObj });
            SetWalletBusy(false);
            return ok;
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Failed to parse network details: {ex.Message}");
            SetWalletBusy(false);
            return Result<WalletConnection>.Err(StatusCode.WALLET_PARSING_ERROR, ex.Message);
        }
    }

    public static void DisconnectWallet()
    {
        address = null;
        networkDetails = null;
    }
    static async Task<Result<bool>> CheckWallet()
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
            return Result<bool>.Err(StatusCode.WALLET_NOT_AVAILABLE, "Wallet not available");
        };
        Debug.Log("CheckWallet() completed");
        return Result<bool>.Ok(true);
    }

    static async Task<Result<string>> GetAddress()
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
            return Result<string>.Err(StatusCode.WALLET_ADDRESS_MISSING, "Wallet address missing");
        }
        Debug.Log($"GetAddress() completed with data {getAddressRes.data}");
        return Result<string>.Ok(getAddressRes.data);
    }

    static async Task<Result<string>> GetNetworkDetails()
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
            return Result<string>.Err(StatusCode.WALLET_NETWORK_DETAILS_ERROR, "Wallet network details error");
        }
        Debug.Log($"GetNetworkDetails() completed with data {getNetworkDetailsRes.data}");
        return Result<string>.Ok(getNetworkDetailsRes.data);
    }

    public static async Task<Result<string>> SignTransaction(string unsignedTransactionEnvelope, string networkPassphrase)
    {
        if (signTransactionTaskSource != null && !signTransactionTaskSource.Task.IsCompleted)
        {
            throw new Exception("SignTransaction() is already in progress");
        }
        SetWalletBusy(true);
        signTransactionTaskSource = new TaskCompletionSource<JSResponse>();
        JSSignTransaction(unsignedTransactionEnvelope, networkPassphrase);
        JSResponse signTransactionRes = await signTransactionTaskSource.Task;
        signTransactionTaskSource = null;
        if (signTransactionRes.code != 1)
        {
            // TODO: make this more robust 
            Debug.Log("SignTransaction() failed with code " + signTransactionRes.code);
            SetWalletBusy(false);
            return Result<string>.Err(StatusCode.WALLET_SIGNING_ERROR, $"failed to sign transaction {signTransactionRes.data}");
        }
        Debug.Log($"SignTransaction() completed with data {signTransactionRes.data}");
        var result = Result<string>.Ok(signTransactionRes.data);
        SetWalletBusy(false);
        return result;
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
                "_JSSignTransaction" => signTransactionTaskSource,
                _ => throw new Exception($"StellarResponse() function not found {response}"),
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