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
    static extern void JSInvokeContractFunction(string address, string contractAddress, string contractFunction, string dataJson);

    public static string address;
    public static NetworkDetails networkDetails;
    
    public static bool webGL;
    
    public static WalletManager instance;
    
    
    // wrapper tasks
    static TaskCompletionSource<JSResponse> checkWalletTaskSource;
    static TaskCompletionSource<JSResponse> getAddressTaskSource;
    static TaskCompletionSource<JSResponse> getNetworkDetailsTaskSource;
    static TaskCompletionSource<JSResponse> invokeContractFunctionTaskSource;
    
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
        Result<bool> check = await CheckWallet();
        address = null;
        networkDetails = null;
        if (check.IsError)
        {
            Debug.LogWarning("Wallet could not be found");
            return Result<WalletConnection>.Err(check);
        }
        Result<string> addrRes = await GetAddress();
        if (addrRes.IsError)
        {
            Debug.LogWarning("Address not found");
            return Result<WalletConnection>.Err(addrRes);
        }
        address = addrRes.Value;
        Result<string> ndRes = await GetNetworkDetails();
        if (ndRes.IsError)
        {
            Debug.LogWarning("Network details not found");
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
            return Result<WalletConnection>.Ok(new WalletConnection { address = address, networkDetails = networkDetailsObj });
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Failed to parse network details: {ex.Message}");
            return Result<WalletConnection>.Err(StatusCode.WALLET_PARSING_ERROR, ex.Message);
        }
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
    
    public static async Task<Result<string>> InvokeContractFunction(string function, string data)
    {
        if (invokeContractFunctionTaskSource != null && !invokeContractFunctionTaskSource.Task.IsCompleted)
        {
            throw new Exception("InvokeContractFunction() is already in progress");
        }
        if (!webGL)
        {
            return Result<string>.Err(StatusCode.WALLET_NOT_AVAILABLE, "Not running in WebGL context");
        }
        if (string.IsNullOrEmpty(address))
        {
            return Result<string>.Err(StatusCode.WALLET_ADDRESS_MISSING, "Wallet address not set");
        }
        string contractAddress = StellarManager.GetContractAddress();
        if (string.IsNullOrEmpty(contractAddress))
        {
            return Result<string>.Err(StatusCode.CONTRACT_ERROR, "Contract address not set");
        }
        invokeContractFunctionTaskSource = new TaskCompletionSource<JSResponse>();
        try
        {
            JSInvokeContractFunction(address, contractAddress, function, string.IsNullOrEmpty(data) ? "{}" : data);
        }
        catch (Exception e)
        {
            invokeContractFunctionTaskSource = null;
            return Result<string>.Err(StatusCode.OTHER_ERROR, e.Message);
        }
        JSResponse res = await invokeContractFunctionTaskSource.Task;
        invokeContractFunctionTaskSource = null;
        if (res.code == 1)
        {
            return Result<string>.Ok(res.data);
        }
        StatusCode map = res.code switch
        {
            -1 => StatusCode.SIMULATION_FAILED,
            -2 => StatusCode.WALLET_ERROR,
            -3 => StatusCode.TRANSACTION_SEND_FAILED,
            -4 => StatusCode.TRANSACTION_FAILED,
            -666 => StatusCode.OTHER_ERROR,
            _ => StatusCode.OTHER_ERROR,
        };
        return Result<string>.Err(map, res.data);
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
                "_JSInvokeContractFunction" => invokeContractFunctionTaskSource,
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