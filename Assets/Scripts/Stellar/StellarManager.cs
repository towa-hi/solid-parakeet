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
    
    public string contract;

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
        currentUser = new User
        {
            index = currentAddress,
            name = "uninitialized",
            games_completed = 0,
        };
        OnCurrentUserChanged?.Invoke();
        return true;
    }
    
    public async Task<bool> TestFunction()
    {
        StellarDotnet stellar = new StellarDotnet("SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV", "CDB6NMVSPVZ54F4O74ZQDGMUZEYUE2MROK4JP4LBCGYNNTK6AQKWULOY");
        // NestedTestReq nestedTestReq = new NestedTestReq()
        // {
        //     flat = new FlatTestReq()
        //     {
        //         number = 1,
        //         word = "flat word",
        //     },
        //     number = 2,
        //     word = "nested word",
        // };
        // await stellar.TestFunction("nested_param_test", nestedTestReq);
        SendInviteReq sendInviteReq = new SendInviteReq
        {
            guest_address = "guest address",
            host_address = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56",
            ledgers_until_expiration = 10,
            parameters = new Contract.LobbyParameters
            {
                board_def = new Contract.BoardDef
                {
                    default_max_pawns = new MaxPawns[]
                    {
                        new MaxPawns
                        {
                            max = 9,
                            rank = 5,
                        }
                    },
                    is_hex = false,
                    name = "name",
                    size = new Pos(new Vector2Int(2,3)),
                    tiles = new Contract.Tile[]
                    {
                        new Contract.Tile
                        {
                            auto_setup_zone = 0,
                            is_passable = false,
                            pos = new Pos(new Vector2Int(4,5)),
                            setup_team = 0
                        }
                    }
                },
                dev_mode = false,
                max_pawns = new MaxPawns[]
                {
                    new MaxPawns
                    {
                        max = 19,
                        rank = 15,
                    },
                },
                must_fill_all_tiles = false,
                security_mode = false
            },
        };
        SCVal xdr1 = sendInviteReq.ToScvMap();
        
        SCVal xdr2 = await stellar.TestFunction("test_send_invite_req", sendInviteReq);
        SendInviteReq result = SCUtility.SCValToNative<SendInviteReq>(xdr2);
        Debug.Log(SCValXdr.EncodeToBase64(xdr1));
        Debug.Log(SCValXdr.EncodeToBase64(xdr2));
        if (result.Equals(sendInviteReq))
        {
            Debug.Log("hash same");
        }
        else
        {
            Debug.Log("hash diff");
        }
         
        return true;
    }


    public async Task<bool> SecondTestFunction(string guestAddress)
    {
        
        return true;
    }
    
    #endregion
    #region helper

    async Task<int> StartLobby()
    {
        
        StellarResponseData response = await InvokeContractFunction(currentUser.Value.index, contract, "test_get_lobby", "");
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