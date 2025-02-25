using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ContractTypes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stellar;
using Stellar.RPC;
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
    
    public string contract = "CBHNTQX7TBVXR2DJC6VCMYJLBCO4GEHYTQHB6MLCHT2725FZNL2IWLTF";
    
    // wrapper tasks
    TaskCompletionSource<StellarResponseData> checkWalletTaskSource;
    TaskCompletionSource<StellarResponseData> setCurrentAddressFromFreighterTaskSource;
    TaskCompletionSource<StellarResponseData> getDataTaskSource;
    TaskCompletionSource<StellarResponseData> invokeContractFunctionTaskSource;
    TaskCompletionSource<StellarResponseData> getEventsTaskSource;
    
    // state
    public bool webGLBuild = false;
    public User? currentUser;
    public event Action<bool> OnWalletConnected;
    public event Action OnCurrentUserChanged;
    public event Action<string> OnContractChanged;

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
    #region pub

    public void SetContract(string inContract)
    {
        contract = inContract;
        OnContractChanged?.Invoke(contract);
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
        string currentAddress = getAddressResult.data;
        (int getUserDataCode, User? userData) = await GetUserData(currentAddress);
        if (getUserDataCode != 1)
        {
            return false;
        }
        if (!userData.HasValue)
        {
            Debug.Log("This user is new and needs to be registered");
            string defaultName = "kiki";
            (int registerUserCode, User? newUserData) = await RegisterUser(currentAddress, defaultName);
            if (registerUserCode != 1)
            {
                currentUser = null;
                return false;
            }
            currentUser = newUserData.Value;
            OnCurrentUserChanged?.Invoke();
            return true;
        }
        currentUser = userData.Value;
        OnCurrentUserChanged?.Invoke();
        return true;
    }
    
    public async Task<bool> TestFunction()
    {
        Debug.Log("registering name");
        if (currentUser == null)
        {
            Debug.Log("TestFunction() didnt work because currentUser must exist");
            return false;
        }
        int num = Random.Range(0, 100);
        string newName = "kiki" + num.ToString();
        (int registerUserCode, User? newUserData) = await RegisterUser(currentUser.Value.user_id, newName);
        if (registerUserCode != 1)
        {
            currentUser = null;
            return false;
        }
        currentUser = newUserData.Value;
        OnCurrentUserChanged?.Invoke();
        return true;
    }

    public async Task<bool> SecondTestFunction(string guestAddress)
    {
        await StartLobby();
        return true; 
    }

    #endregion
    #region helper
    
    async Task<(int, User?)> GetUserData(string address)
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
            User user = new User(entry);
            return (response.code, user);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    async Task<(int, User?)> RegisterUser(string address, string userName)
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

    async Task<int> StartLobby()
    {
        
        StellarResponseData response = await InvokeContractFunction(currentUser.Value.user_id, contract, "test_get_lobby", "");
        // TODO: finish this
        return 0;
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
        JSGetData(contract, keyType, keyValue);
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
    
    async Task<bool> GetEvents(string filter, string contractAddress, string topic)
    {
        Debug.Log("GetEvents() started");
        JSGetEvents(filter, contractAddress, topic);
        getEventsTaskSource = new TaskCompletionSource<StellarResponseData>();
        StellarResponseData response = await getEventsTaskSource.Task;
        getEventsTaskSource = null;
        Debug.Log(response.data);
        return true;
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

public class RStartLobbyRequestData
{
    public ContractTypes.Lobby lobby;
}

namespace ContractTypes
{
// ReSharper disable InconsistentNaming

    public struct User
    {
        public string user_id;
        public string name;
        public int games_played;
        public string current_lobby;

        public User(string jsonString)
        {
            JObject json = JObject.Parse(jsonString);
            user_id = json["user_id"]?.ToString() ?? "";
            name = json["name"]?.ToString() ?? "";
            games_played = json["games_played"]?.ToObject<int>() ?? 0;
            current_lobby = json["current_lobby"]?.ToString() == "void" ? null : json["current_lobby"]?.ToString();
        }
    }

    public struct Vector2Int
    {
        public int x;
        public int y;

        public Vector2Int(UnityEngine.Vector2Int vector)
        {
            x = vector.x;
            y = vector.y;
        }
    }
    
    public struct UserState
    {
        public string user_id;
        public int team;
    }

    public struct PawnDef
    {
        public string def_id;
        public int rank;
        public string name;
        public int power;
        public int movement_range;

    }
    
    public struct Tile
    {
        public Vector2Int pos;
        public bool is_passable;
        public int setup_team;
        public int auto_setup_zone;

        public Tile(global::Tile tile)
        {
            pos = new Vector2Int(tile.pos);
            is_passable = tile.isPassable;
            setup_team = (int)tile.setupTeam;
            auto_setup_zone = tile.autoSetupZone;
        }
        
    }
    
    public struct PawnCommitment
    {
        string user_id;
        string pawn_id;
        Vector2Int pos;
        string def_hidden;
    }
    
    public struct SetupCommitment
    {
        string user_id;
        List<PawnCommitment> pawn_positions;
    }
    
    public struct Pawn
    {
        public string pawn_id;
        public string user;
        public int team;
        public string def_hidden;
        public string def_key;
        public PawnDef def;
        public Vector2Int pos;
        public bool is_alive;
        public bool is_moved;
        public bool is_revealed;
    }
    
    public struct Lobby
    {
        public string lobby_id;
        public string host;
        public RBoardDef board_def;
        public bool must_fill_all_tiles;
        public Dictionary<int, int> max_pawns;
        public bool is_secure;
        public List<UserState> user_states;
        public int game_end_state;
        public List<Pawn> pawns;

        public Lobby(string host_address, string guest_address, LobbyParameters parameters)
        {
            lobby_id = "UNDEFINED";
            host = host_address;
            board_def =  new RBoardDef(parameters.board);
            must_fill_all_tiles = parameters.mustFillAllTiles;
            max_pawns = new Dictionary<int, int>();
            for (int i = 0; i < parameters.maxPawns.Length; i++)
            {
                max_pawns[(int)parameters.maxPawns[i].rank] = parameters.maxPawns[i].max;
            }
            is_secure = false;
            user_states = new List<UserState>();
            // TODO: add user states
            game_end_state = 0;
            pawns = new List<Pawn>();
        }
    }


    public struct RBoardDef
    {
        public string name;
        public Vector2Int size;
        public Dictionary<Vector2Int, Tile> tiles;
        public bool isHex;
        public Dictionary<int, int> default_max_pawns;

        public RBoardDef(BoardDef boardDef)
        {
            name = boardDef.boardName;
            size = new Vector2Int(boardDef.boardSize);
            tiles = new Dictionary<Vector2Int, Tile>();
            for (int i = 0; i < boardDef.tiles.Length; i++)
            {
                Tile tile = new(boardDef.tiles[i]);
                tiles[tile.pos] = tile;
            }
            isHex = boardDef.isHex;
            default_max_pawns = new Dictionary<int, int>();
            for (int i = 0; i < boardDef.maxPawns.Length; i++)
            {
                default_max_pawns[(int)boardDef.maxPawns[i].rank] = boardDef.maxPawns[i].max;
            }
        }
    }


    // ReSharper restore InconsistentNaming

}

public enum NetworkStatus
{
    
}