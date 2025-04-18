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

    public static string address;
    
    public static bool webGL;
    
    // wrapper tasks
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> getAddressTaskSource;
    TaskCompletionSource<StellarResponseData> getDataTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    TaskCompletionSource<StellarResponseData> getEventsTaskSource;
    
    public JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    void Awake()
    {
#if UNITY_WEBGL
        webGL = true;
#endif
    }

    public async Task<bool> OnConnectWallet()
    {
        bool walletExists = await CheckWallet();
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
        return true;
    }
    
    async Task<bool> CheckWallet()
    {
        if (checkWalletTaskSource != null && !checkWalletTaskSource.Task.IsCompleted)
        {
            throw new Exception("CheckWallet() is already in progress");
        }
        checkWalletTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSCheckWallet();
        StellarResponseData checkWalletRes = await checkWalletTaskSource.Task;
        checkWalletTaskSource = null;
        if (checkWalletRes.code != 1)
        {
            Debug.Log("CheckWallet() failed with code " + checkWalletRes.code);
            return false;
        };
        Debug.Log("CheckWallet() completed");
        return true;
    }

    async Task<string> GetAddress()
    {
        if (getAddressTaskSource != null && !getAddressTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddressFromFreighter() is already in progress");
        }
        getAddressTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetFreighterAddress();
        StellarResponseData getAddressRes = await getAddressTaskSource.Task;
        getAddressTaskSource = null;
        if (getAddressRes.code != 1)
        {
            Debug.Log("GetAddress() failed with code " + getAddressRes.code);
            return null;
        }
        Debug.Log("GetAddress() completed");
        return getAddressRes.data;
    }
    
    public void StellarResponse(string json)
    {
        try
        {
            StellarResponseData response = JsonUtility.FromJson<StellarResponseData>(json);
            if (response.code == -666)
            {
                throw new Exception($"StellarResponse() got unspecified error: {response}");
            }
            TaskCompletionSource<StellarResponseData> task = response.function switch
            {
                "_JSCheckWallet" => checkWalletTaskSource,
                "_JSGetFreighterAddress" => getAddressTaskSource,
                "_JSGetData" => getDataTaskSource,
                "_JSInvokeContractFunction" => invokeContractFunctionTaskSource,
                "_JSGetEvents" => getEventsTaskSource,
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
