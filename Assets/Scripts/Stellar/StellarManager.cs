using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Contract;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using Random = UnityEngine.Random;

public class StellarManager : MonoBehaviour
{
    [DllImport("__Internal")]
    static extern void JSCheckWallet();

    [DllImport("__Internal")]
    static extern void JSGetFreighterAddress();

    [DllImport("__Internal")]
    static extern void JSGetData(string contractAddressPtr, string keyTypePtr, string keyValuePtr);
    
    [DllImport("__Internal")]
    static extern void JSInvokeContractFunction(string addressPtr, string contractAddressPtr, string contractFunctionPtr, string dataPtr);

    [DllImport("__Internal")]
    static extern void JSGetEvents(string filterPtr, string contractAddressPtr, string topicPtr);
    
    public BoardDef testBoardDef;
    
    // wrapper tasks
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> setCurrentAddressFromFreighterTaskSource;
    TaskCompletionSource<StellarResponseData> getDataTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    TaskCompletionSource<StellarResponseData> getEventsTaskSource;
    
    // state
    public bool webGLBuild = false;
    public User? currentUser;
    public StellarDotnet stellar;
    
    public event Action<bool> OnWalletConnected;
    public event Action OnCurrentUserChanged;
    public event Action<string> OnContractChanged;
    public event Action<bool> OnInviteSent;
    public event Action<List<Invite>> OnInviteCheck;

    public static string testContract = "CDGP3JUJDNHW6ZA7KQSEWHQBYBTY4HAQ7ZHNQDXOGEYIJUI5YFXYVKKN";
    public static string testGuest = "GD6APTUYGQJUR2Q5QQGKCZNBWE7BVLTZQAJ4LRX5ZU55ODB65FMJBGGO";
    public static string testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";

    JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    void Awake()
    {
        stellar = new StellarDotnet(testHostSneed, testContract);
        #if UNITY_WEBGL
            webGLBuild = true;
        #endif
    }
    #region pub

    public void SetContract(string contractId)
    {
        //stellar.SetContractId(contractId);
        OnContractChanged?.Invoke(contractId);
    }
    
    public async Task<bool> OnConnectWallet()
    {
        if (!webGLBuild)
        {
            return false;
        }
        currentUser = null;
        OnCurrentUserChanged?.Invoke();
        StellarResponseData checkWalletResult = await CheckWallet();
        if (checkWalletResult.code != 1)
        {
            return false;
        };
        StellarResponseData getAddressResult = await GetAddressFromFreighter();
        if (getAddressResult.code != 1)
        {
            return false;
        };
        currentUser = new User
        {
            current_lobby = 0,
            games_completed = 0,
        };
        OnCurrentUserChanged?.Invoke();
        return true;
    }
    
    #endregion
    #region helper
    
    
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
    
    async Task<StellarResponseData> GetAddressFromFreighter()
    {
        if (setCurrentAddressFromFreighterTaskSource != null && !setCurrentAddressFromFreighterTaskSource.Task.IsCompleted)
        {
            throw new Exception("GetAddressFromFreighter() is already in progress");
        }
        setCurrentAddressFromFreighterTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetFreighterAddress();
        StellarResponseData response = await setCurrentAddressFromFreighterTaskSource.Task;
        setCurrentAddressFromFreighterTaskSource = null;
        // NOTE: we retry once because of a bug where if a user isn't signed in freighter returns empty data
        if (response.data == "")
        {
            setCurrentAddressFromFreighterTaskSource = new TaskCompletionSource<StellarResponseData>();
            JSGetFreighterAddress();
            StellarResponseData response2 = await setCurrentAddressFromFreighterTaskSource.Task;
            setCurrentAddressFromFreighterTaskSource = null;
            if (response2.code != 1 || response2.data == "")
            {
                throw new Exception("GetAddressFromFreighter() returned an empty address again");
            }
            else
            {
                return response2;
            }
        }
        return response;
    }
    
    #endregion
    #region response handling
    
    // NOTE: called from JS only
    public void StellarResponse(string json)
    {
        try
        {
            // TODO: use newtonsoft here
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
    
    #endregion
}

[Serializable]
public class StellarResponseData
{
    public string function;
    public int code;
    public string data;
}