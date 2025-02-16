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
    public bool registered = false;
    public string currentAddress = null;
    public event Action<bool> OnWalletConnected; 

    JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
    {
        ContractResolver = (IContractResolver) new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };
    
    #region Public
    
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
            JObject userResponse = await GetDataEntriesJson("User");
            
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

    async Task<JObject> GetDataEntriesJson(string key)
    {
        StellarResponseData response = await GetData(key, currentAddress);
        JObject json = JObject.Parse(response.data);
        return json;
    }
    
    public async Task<bool> TestFunction()
    {
        Debug.Log("TestFunction started");
        //JsonTest();
        //return true;
        #if UNITY_WEBGL
            // quick address check
            await SetCurrentAddressFromFreighter();
            StellarResponseData response = await GetData("User", currentAddress);
            string dataXdrString = response.data;
            
            JObject jsonObject = JObject.Parse(response.data);
            if (jsonObject["entries"] == null)
            {
                throw new Exception("jsonObject entries is null");
            }
            string[] xdrEntryStrings = jsonObject["entries"].ToObject<string[]>();
            foreach (string xdrEntryString in xdrEntryStrings)
            {
                byte[] bytes = Convert.FromBase64String(xdrEntryString);
                MemoryStream memoryStream = new MemoryStream(bytes);
                XdrReader xdrReader = new XdrReader(memoryStream);
                LedgerEntry entryObject = LedgerEntryXdr.Decode(xdrReader);
                 
                Debug.Log(entryObject.ToString());
            }
            byte[] xdrBytes = Convert.FromBase64String(response.data);
            
            //Debug.Log(data);
            
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
    
    async Task<StellarResponseData> GetData(string keyType, string keyValue)
    {
        if (string.IsNullOrEmpty(currentAddress)) throw new Exception("GetData() called but no address yet");
        getDataTaskSource = new TaskCompletionSource<StellarResponseData>();
        JSGetData(currentAddress, contract, keyType, keyValue);
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

// ReSharper disable InconsistentNaming
public struct RUser
{
    string user_id;
    string name;
    int games_played;
    [CanBeNull] string current_lobby;

    // public RUser(JObject json)
    // {
    //     
    // }
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