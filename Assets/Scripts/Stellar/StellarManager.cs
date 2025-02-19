using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stellar;
using Stellar.RPC;

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
    public bool webGLBuild = false;
    public RUser? currentUser;
    public event Action<bool> OnWalletConnected;
    public event Action OnCurrentUserChanged;

    JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    void Awake()
    {
        #if UNITY_WEBGL
            webGLBuild = true;
        #endif
    }
    #region Public
    
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
        string currentAddress = getAddressResult.data;
        (int getUserDataCode, RUser? userData) = await GetUserData(currentAddress);
        if (getUserDataCode != 1)
        {
            return false;
        }
        Debug.Log(userData.HasValue);
        if (!userData.HasValue)
        {
            Debug.Log("This user is new and needs to be registered");
            string defaultName = "kiki";
            (int registerUserCode, RUser? newUserData) = await RegisterUser(currentAddress, defaultName);
            if (registerUserCode != 1)
            {
                currentUser = null;
                return false;
            }
            currentUser = newUserData.Value;
            OnCurrentUserChanged?.Invoke();
            return true;
        }
        else
        {
            Debug.Log(userData.Value);
        }
        currentUser = userData.Value;
        OnCurrentUserChanged?.Invoke();
        return true;
    }
    
    public async Task<bool> TestFunction()
    {
        if (!webGLBuild)
        {
            return false;
        }
        // Debug.Log("TestFunction started");
        // // quick address check
        // await GetAddressFromFreighter();
        // StellarResponseData response = await GetData("User", currentAddress);
        // string dataString = response.data;
        // Debug.Log(dataString);
        // JObject jsonObject = JObject.Parse(dataString);
        // if (jsonObject["entries"] == null)
        // {
        //     throw new Exception("jsonObject entries is null");
        // }
        //
        //Debug.Log(data);
        
        return true;
    }

    #endregion
    #region helper
    
    async Task<(int, RUser?)> GetUserData(string address)
    {
        StellarResponseData response = await GetData(address, "User", address);
        if (response.code != 1)
        {
            return (response.code, null);
        }
        Debug.Log(response.data);
        try
        {
            JObject jsonEntries = JObject.Parse(response.data);
            if (!jsonEntries["entries"].HasValues)
            {
                Debug.Log("entries has no data");
                return (response.code, null);
            }
            string entry = jsonEntries["entries"].First.ToString();
            RUser user = new RUser(entry);
            return (response.code, user);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    async Task<(int, RUser?)> RegisterUser(string address, string userName)
    {
        StellarResponseData response = await InvokeContractFunction(address, contract, "register", userName);
        if (response.code != 1)
        {
            return (response.code, null);
        }
        try
        {
            Debug.Log(response.data);
            JObject json = JObject.Parse(response.data);
            
            return (response.code, null);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }

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
    
    async Task<StellarResponseData> GetData(string address, string keyType, string keyValue)
    {
        getDataTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetData(address, contract, keyType, keyValue);
        StellarResponseData getDataRes = await getDataTaskSource.Task;
        return getDataRes;
    }
    
    async Task<StellarResponseData> InvokeContractFunction(string address, string contractAddress, string function, string data)
    {
        if (invokeContractFunctionTaskSource != null && !invokeContractFunctionTaskSource.Task.IsCompleted)
        {
            throw new Exception("InvokeContractFunction() is already in progress");
        }
        invokeContractFunctionTaskSource = new TaskCompletionSource<StellarResponseData>();
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

// ReSharper disable InconsistentNaming
public struct RUser
{
    public string user_id;
    public string name;
    public int games_played;
    public string current_lobby;

    public RUser(string jsonString)
    {
        JObject json = JObject.Parse(jsonString);
        user_id = json["user_id"]?.ToString() ?? "";
        name = json["name"]?.ToString() ?? "";
        games_played = json["games_played"]?.ToObject<int>() ?? 0;
        current_lobby = json["current_lobby"]?.ToString() == "void" ? null : json["current_lobby"]?.ToString();

    }
}

public struct RUserState
{
    string user_id;
    int team;
}

public struct RLobby
{
    string lobby_id;
    string host;
    RBoardDef board_def;
    bool must_fill_all_tiles;
    (int, int)[] max_pawns;
    bool is_secure;
    RUserState[] user_states;
    int game_end_state;
    (string, RPawn) pawns;
}

public struct RSetupCommitment
{
    string user_id;
    RPawnCommitment[] pawn_positions;
}

public struct RPawnCommitment
{
    string user_id;
    string pawn_id;
    Vector2Int pos;
    string def_hidden;
}

public struct RBoardDef
{
    string name;
    Vector2Int size;
    (Vector2Int, RTile) tiles;
    bool isHex;
    (int, int) default_max_pawns;
}

public struct RTile
{
    Vector2Int pos;
    bool is_passable;
    int setup_team;
    int auto_setup_zone;
}
public struct RPawn
{
    string pawn_id;
    string user;
    int team;
    string def_hidden;
    string def_key;
    RPawnDef? def;
    Vector2Int pos;
    bool is_alive;
    bool is_moved;
    bool is_revealed;
}

public struct RAddress
{
    string address;
}

public struct RPawnDef
{
    string def_id;
    int rank;
    string name;
    int power;
    int movement_range;

}
// ReSharper restore InconsistentNaming

public enum NetworkStatus
{
    
}