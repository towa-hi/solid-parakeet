using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void SendUnityMessage();
    
    [DllImport("__Internal")]
    static extern void JSCheckWallet();

    [DllImport("__Internal")]
    static extern void JSGetAddress();
    
    [DllImport("__Internal")] 
    static extern void JSInvokeContractFunction(string addressPtr, string contractAddressPtr, string contractFunctionPtr, string dataPtr, int transactionTimeoutSec, int pingFrequencyMS);
    
    string contract = "CCK2WEL5BBKDIMEIGMBEIS4CQLEI3D6CI5EFH52J4DOKNKR5AUR5UTYZ";
    //public string address = "";
    
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> getAddressTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    
    public async Task<StellarResponseData> CheckWallet()
    {
#if UNITY_WEBGL
        if (checkWalletTaskSource != null && !checkWalletTaskSource.Task.IsCompleted)
        {
            throw new Exception("CheckWallet() is already in progress");
        }
        checkWalletTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSCheckWallet();
        StellarResponseData checkWalletRes = await checkWalletTaskSource.Task;
        checkWalletTaskSource = null;
        if (checkWalletRes.code != 1) throw new Exception("FUCK");
        StellarResponseData getAddressRes = await GetAddress();
        if (getAddressRes.code != 1) throw new Exception("WEWLAD");
        string address = getAddressRes.data;
        StellarResponseData invokeContractFunctionRes = await InvokeContractFunction(address, contract, "register", "newname");
        Debug.Log(invokeContractFunctionRes.data);
        return checkWalletRes;
#else
        throw new Exception("not WebGL")
#endif
    }
    
    public async Task<StellarResponseData> GetAddress()
    {
#if UNITY_WEBGL
        if (getAddressTaskSource != null && !getAddressTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddress() is already in progress");
        }
        getAddressTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetAddress();
        StellarResponseData response = await getAddressTaskSource.Task;
        getAddressTaskSource = null;
        return response;
#else
        throw new Exception("not WebGL")
#endif
    }
    
    async Task<StellarResponseData> InvokeContractFunction(string address, string contractAddress, string function, string data)
    {
#if UNITY_WEBGL
        if (invokeContractFunctionTaskSource != null && !invokeContractFunctionTaskSource.Task.IsCompleted)
        {
            throw new Exception("InvokeContractFunction() is already in progress");
        }
        invokeContractFunctionTaskSource = new TaskCompletionSource<StellarResponseData>();
        Debug.Log($"contract address: {contractAddress}");
        JSInvokeContractFunction(address, contractAddress, function, data, 100, 2000);
        StellarResponseData response = await invokeContractFunctionTaskSource.Task;
        return response;
#else
        throw new Exception("not WebGL")
#endif
    }

    public void StellarResponse(string json)
    {
        try
        {
            StellarResponseData response = JsonUtility.FromJson<StellarResponseData>(json);
            switch (response.function)
            {
                case "_JSCheckWallet":
                    checkWalletTaskSource.SetResult(response);
                    break;
                case "_JSGetAddress":
                    getAddressTaskSource.SetResult(response);
                    break;
                case "_JSInvokeContractFunction":
                    invokeContractFunctionTaskSource.SetResult(response);
                    break;
                default:
                    throw new Exception($"function not found {response.function}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
