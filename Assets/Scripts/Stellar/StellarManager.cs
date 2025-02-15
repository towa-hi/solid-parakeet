using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void JSCheckWallet();

    [DllImport("__Internal")]
    static extern void JSGetAddress();

    [DllImport("__Internal")]
    static extern void JSGetUser(string addressPtr, string contractAddressPtr);
    
    [DllImport("__Internal")]
    static extern void JSInvokeContractFunction(string addressPtr, string contractAddressPtr, string contractFunctionPtr, string dataPtr);
    
    string contract = "CCK2WEL5BBKDIMEIGMBEIS4CQLEI3D6CI5EFH52J4DOKNKR5AUR5UTYZ";
    
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> getAddressTaskSource;
    TaskCompletionSource<StellarResponseData> getUserTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    
    // state
    public bool registered = false;
    public string currentAddress = "";
    public event Action<bool> OnWalletConnected; 

    public async Task<bool> OnConnectWallet()
    {
#if UNITY_WEBGL
        string name = "wewlad";
        StellarResponseData checkWalletResult = await CheckWallet();
        if (checkWalletResult.code != 1)
        {
            OnWalletConnected?.Invoke(false);
            return false;
        };
        StellarResponseData getAddressResult = await GetAddress();
        if (getAddressResult.code != 1)
        {
            OnWalletConnected?.Invoke(false);
            return false;
        };
        currentAddress = getAddressResult.data;
        StellarResponseData invokeRegisterResult = await InvokeContractFunction(getAddressResult.data, contract, "register", name);
        if (invokeRegisterResult.code != 1)
        {
            OnWalletConnected?.Invoke(false);
            return false;
        };
        registered = true;
        Debug.Log("OnConnectWallet completed");
        OnWalletConnected?.Invoke(true);
        return true;
#else
        throw new Exception("not WebGL")
#endif
    }

    public async Task<bool> TestFunction()
    {
#if UNITY_WEBGL
        if (!registered) return false;
        getUserTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetUser(currentAddress, contract);
        StellarResponseData getUserRes = await getUserTaskSource.Task;
        Debug.Log(getUserRes.code);
        return true;
#else
        throw new Exception("not WebGL")
#endif
    }
    
    async Task<StellarResponseData> CheckWallet()
    {
        if (checkWalletTaskSource != null && !checkWalletTaskSource.Task.IsCompleted)
        {
            throw new Exception("CheckWallet() is already in progress");
        }
        checkWalletTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSCheckWallet();
        StellarResponseData checkWalletRes = await checkWalletTaskSource.Task;
        checkWalletTaskSource = null;
        return checkWalletRes;
    }
    
    async Task<StellarResponseData> GetAddress()
    {
        if (getAddressTaskSource != null && !getAddressTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddress() is already in progress");
        }
        getAddressTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetAddress();
        StellarResponseData response = await getAddressTaskSource.Task;
        // TODO: fix the freighter returning empty address when not logged in bug by running twice if detected
        getAddressTaskSource = null;
        return response;
    }
    
    async Task<StellarResponseData> InvokeContractFunction(string address, string contractAddress, string function, string data)
    {
        if (invokeContractFunctionTaskSource != null && !invokeContractFunctionTaskSource.Task.IsCompleted)
        {
            throw new Exception("InvokeContractFunction() is already in progress");
        }
        invokeContractFunctionTaskSource = new TaskCompletionSource<StellarResponseData>();
        Debug.Log($"contract address: {contractAddress}");
        JSInvokeContractFunction(address, contractAddress, function, data);
        StellarResponseData response = await invokeContractFunctionTaskSource.Task;
        return response;
    }
    
    // NOTE: called from JS only
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
                "_JSGetAddress" => getAddressTaskSource,
                "_JSGetUser" => getUserTaskSource,
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

[Serializable]
public class StellarResponseData
{
    public string function;
    public int code;
    public string data;
}
