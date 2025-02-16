using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void JSCheckWallet();

    [DllImport("__Internal")]
    static extern void JSGetFreighterAddress();

    [DllImport("__Internal")]
    static extern void JSGetData(string userAddressPtr, string contractAddressPtr, string keyTypePtr, string keyValuePtr);
    
    [DllImport("__Internal")]
    static extern void JSInvokeContractFunction(string addressPtr, string contractAddressPtr, string contractFunctionPtr, string dataPtr);
    
    string contract = "CCK2WEL5BBKDIMEIGMBEIS4CQLEI3D6CI5EFH52J4DOKNKR5AUR5UTYZ";
    
    // wrapper tasks
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> setCurrentAddressFromFreighterTaskSource;
    TaskCompletionSource<StellarResponseData> getDataTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    
    // state
    public bool registered = false;
    public string currentAddress = null;
    public event Action<bool> OnWalletConnected; 

    #region Public

    public void JsonTest()
    {
        string val = @"{""entries"":[{""contract_data"":{""ext"":""v0"",""contract"":""CCK2WEL5BBKDIMEIGMBEIS4CQLEI3D6CI5EFH52J4DOKNKR5AUR5UTYZ"",""key"":{""vec"":[{""symbol"":""User""},{""address"":""GAAFFCDB2UOOS7YHGL3M3YGTMKTQA35II64JKSGPEBXVZ5BRABDY65HH""}]},""durability"":""persistent"",""val"":{""map"":[{""key"":{""symbol"":""current_lobby""},""val"":""void""},{""key"":{""symbol"":""games_played""},""val"":{""u32"":0}},{""key"":{""symbol"":""name""},""val"":{""string"":""wewlad""}},{""key"":{""symbol"":""user_id""},""val"":{""address"":""GAAFFCDB2UOOS7YHGL3M3YGTMKTQA35II64JKSGPEBXVZ5BRABDY65HH""}}]}}}]}";
        JObject result = XdrJsonHelper.DeserializeXdrJson(val);
        Debug.Log("JsonTest() completed");
    }
    
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
            StellarResponseData setAddressResult = await SetCurrentAddressFromFreighter();
            if (setAddressResult.code != 1)
            {
                OnWalletConnected?.Invoke(false);
                return false;
            };
            // StellarResponseData invokeRegisterResult = await InvokeContractFunction(getAddressResult.data, contract, "register", name);
            // if (invokeRegisterResult.code != 1)
            // {
            //     OnWalletConnected?.Invoke(false);
            //     return false;
            // };
            // registered = true;
            Debug.Log("OnConnectWallet completed");
            OnWalletConnected?.Invoke(true);
            return true;
        #else
                throw new Exception("not WebGL")
        #endif
    }
    
    public async Task<bool> TestFunction()
    {
        //JsonTest();
        //return true;
        #if UNITY_WEBGL
                // quick address check
                await SetCurrentAddressFromFreighter();
                string data = await GetData("User", currentAddress);
                Debug.Log(data);
                
                return true;
        #else
                throw new Exception("not WebGL")
        #endif
    }
    
    #endregion
    #region wrapper
    
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
    
    async Task<StellarResponseData> SetCurrentAddressFromFreighter()
    {
        if (setCurrentAddressFromFreighterTaskSource != null && !setCurrentAddressFromFreighterTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddress() is already in progress");
        }
        setCurrentAddressFromFreighterTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetFreighterAddress();
        StellarResponseData response = await setCurrentAddressFromFreighterTaskSource.Task;
        setCurrentAddressFromFreighterTaskSource = null;
        if (response.data == "")
        {
            setCurrentAddressFromFreighterTaskSource = new TaskCompletionSource<StellarResponseData>();
            JSGetFreighterAddress();
            StellarResponseData response2 = await setCurrentAddressFromFreighterTaskSource.Task;
            setCurrentAddressFromFreighterTaskSource = null;
            if (response2.code != 1 || response2.data == "")
            {
                throw new Exception("GetAddress() returned an empty address again");
            }
            else
            {
                currentAddress = response2.data;
                return response2;
            }
        }
        currentAddress = response.data;
        return response;
    }
    
    async Task<string> GetData(string keyType, string keyValue)
    {
        if (string.IsNullOrEmpty(currentAddress)) throw new Exception("GetData() called but no address yet");
        getDataTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetData(currentAddress, contract, keyType, keyValue);
        StellarResponseData getDataRes = await getDataTaskSource.Task;
        Debug.Log($"GetData() code: {getDataRes.code}");
        return getDataRes.data;
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
    
    #endregion
    #region response handling
    
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
                "_JSGetFreighterAddress" => setCurrentAddressFromFreighterTaskSource,
                "_JSGetData" => getDataTaskSource,
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
    
    #endregion
    #region deserialization

    void DeserializeUserJson(string json)
    {
        
    }
    
    #endregion
}

[Serializable]
public class StellarResponseData
{
    public string function;
    public int code;
    public string data;
}

