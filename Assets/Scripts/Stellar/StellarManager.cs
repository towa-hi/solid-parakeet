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
using Stellar.Utilities;
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
        // (int getUserDataCode, User? userData) = await GetUserData(currentAddress);
        // if (getUserDataCode != 1)
        // {
        //     return false;
        // }
        // if (!userData.HasValue)
        // {
        //     Debug.Log("This user is new and needs to be registered");
        //     string defaultName = "kiki";
        //     (int registerUserCode, User? newUserData) = await RegisterUser(currentAddress, defaultName);
        //     if (registerUserCode != 1)
        //     {
        //         currentUser = null;
        //         return false;
        //     }
        //     currentUser = newUserData.Value;
        //     OnCurrentUserChanged?.Invoke();
        //     return true;
        // }
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
        MuxedAccount.KeyTypeEd25519 testAccount = MuxedAccount.FromSecretSeed("SBBAF3LZZPQVPPBJKSY2ZE7EF2L3IIWRL7RXQCXVOELS4NQRMNLZN6PB");
        AccountID testAccountId = new AccountID(testAccount.XdrPublicKey);
        string demoContractId = "CDO5UFNRHPMCLFN6NXFPMS22HTQFZQACUZP6S25QUTFIGDFP4HLD3YVN"; // See SorobanExample project in the solution
        HttpClient httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://soroban-testnet.stellar.org");
        StellarRPCClient client = new StellarRPCClient(httpClient);
        
        Network.UseTestNetwork();
        
        // get account info
        LedgerKey accountKey = new LedgerKey.Account()
        {
            account = new LedgerKey.accountStruct()
            {
                accountID = testAccountId
            }
        };
        var encodedAccountKey = LedgerKeyXdr.EncodeToBase64(accountKey);
        var getLedgerEntriesArgs = new GetLedgerEntriesParams()
        {
            Keys = new [] {encodedAccountKey},
        };
        GetLedgerEntriesResult getLedgerEntriesResponse = await client.GetLedgerEntriesAsync(getLedgerEntriesArgs);
        LedgerEntry.dataUnion.Account ledgerEntry = getLedgerEntriesResponse.Entries.First().LedgerEntryData as LedgerEntry.dataUnion.Account;
        AccountEntry accountEntry = ledgerEntry.account;
        
        
        // First, create the nested FlatTestReq struct as an SCMap
        var flatTestReqMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("number") },
                    val = new SCVal.ScvU32() { u32 = 42 }  // Example value
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("word") },
                    val = new SCVal.ScvString() { str = new SCString("hello") }  // Example value
                }
            })
        };

        // Then, create the parent NestedTestReq struct as an SCMap
        var nestedTestReqMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                /*
                 *  MUST BE IN ALPHA ORDER
                 */
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("flat") },
                    val = flatTestReqMap  // Using the nested struct we created above
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("numba") },
                    val = new SCVal.ScvU32() { u32 = 100 }  // Example value
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("word") },
                    val = new SCVal.ScvString() { str = new SCString("world") }  // Example value
                },

            })
        };
        
        
        NestedTestReq nestedTestReq = new()
        {
            numba = 34,
            word = "nested word",
            flat = new FlatTestReq
            {
                number = 21,
                word = "flat word",
            },
        };
        SCVal nested = SCValConverter.NativeToSCVal(nestedTestReq);
        string encoded = SCValXdr.EncodeToBase64(nested);
        string testEncoded = SCValXdr.EncodeToBase64(nestedTestReqMap);
        Debug.Log(encoded);
        Operation nestedParamTestInvocation = new Operation()
        {
            sourceAccount = testAccount,
            body = new Operation.bodyUnion.InvokeHostFunction()
            {
                invokeHostFunctionOp = new InvokeHostFunctionOp()
                {
                    auth = Array.Empty<SorobanAuthorizationEntry>(),
                    hostFunction = new HostFunction.HostFunctionTypeInvokeContract()
                    {
                        invokeContract = new InvokeContractArgs()
                        {
                            contractAddress = new SCAddress.ScAddressTypeContract()
                            {
                                contractId = new Hash(StrKey.DecodeContractId(demoContractId))
                            },
                            functionName = new SCSymbol("nested_param_test"),
                            args = new [] { nested },
                        },
                    },
                },
            },
        };
        Transaction invokeContractTransaction = new Transaction()
        {
            sourceAccount = testAccount,
            fee = 100,
            memo = new Memo.MemoNone(),
            seqNum = accountEntry.seqNum.Increment(),
            cond = new Preconditions.PrecondNone(),
            ext = new Transaction.extUnion.case_0(),
            operations = new [] { nestedParamTestInvocation },
        };
        
        TransactionEnvelope simulateEnvelope = new TransactionEnvelope.EnvelopeTypeTx()
        {
            v1 = new TransactionV1Envelope()
            {
                tx = invokeContractTransaction,
                signatures = Array.Empty<DecoratedSignature>(),
            },
        };
        SimulateTransactionResult simulationResult = await client.SimulateTransactionAsync(new SimulateTransactionParams()
        {
            Transaction = TransactionEnvelopeXdr.EncodeToBase64(simulateEnvelope),
        });

        Transaction assembledTransaction = simulationResult.ApplyTo(invokeContractTransaction);
        DecoratedSignature signature = assembledTransaction.Sign(testAccount);
        TransactionEnvelope sendEnvelope = new TransactionEnvelope.EnvelopeTypeTx()
        {
            v1 = new TransactionV1Envelope()
            {
                tx = assembledTransaction,
                signatures = new[] { signature },
            },
        };
        
        SendTransactionResult result = await client.SendTransactionAsync(new SendTransactionParams
        {
            Transaction = TransactionEnvelopeXdr.EncodeToBase64(sendEnvelope),
        });
        
        return true;
    }

    public async Task<bool> SecondTestFunction(string guestAddress)
    {
        // StellarResponseData testResponse = await GetData(currentUser.Value.index, "TestSendInviteReq","");
        // JObject jsonEntries = JObject.Parse(testResponse.data);
        // string entry = jsonEntries["entries"].First.ToString();
        // Debug.Log("C# got this entry, now sending");
        // Debug.Log(entry);
        //
        // SendInviteReq req = new SendInviteReq
        // {
        //     host_address = currentUser.Value.index,
        //     guest_address = guestAddress,
        //     ledgers_until_expiration = 123,
        //     parameters = new ContractTypes.LobbyParameters
        //     {
        //         board_def = new ContractTypes.BoardDef(testBoardDef),
        //         must_fill_all_tiles = false,
        //         max_pawns = null,
        //         dev_mode = false,
        //         security_mode = false
        //     },
        // };
        // string data = JsonConvert.SerializeObject(req, jsonSettings);
        FlatTestReq flat = new FlatTestReq()
        {
            number = 1,
            word = "bob"
        };
        string flat_json = JsonConvert.SerializeObject(flat, jsonSettings);
        StellarResponseData response = await InvokeContractFunction(currentUser.Value.index, contract, "flat_param_test", flat_json);
        NestedTestReq nested = new NestedTestReq()
        {
            numba = 2,
            word = "nested",
            flat = flat,
        };
        string nested_json = JsonConvert.SerializeObject(nested, jsonSettings);
        StellarResponseData response2 = await InvokeContractFunction(currentUser.Value.index, contract, "nested_param_test", nested_json);

        return true; 
    }

    #endregion
    #region helper
    //
    // async Task<(int, User?)> GetUserData(string address)
    // {
    //     StellarResponseData response = await GetData(address, "User", address);
    //     if (response.code != 1)
    //     {
    //         return (response.code, null);
    //     }
    //     Debug.Log(response.data);
    //     try
    //     {
    //         JObject jsonEntries = JObject.Parse(response.data);
    //         if (!jsonEntries["entries"].HasValues)
    //         {
    //             Debug.Log("entries has no data");
    //             return (response.code, null);
    //         }
    //         string entry = jsonEntries["entries"].First.ToString();
    //         User user = new User(entry);
    //         return (response.code, user);
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogError(e);
    //         throw;
    //     }
    // }

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

public interface XDRCompatable
{
    public SCVal ToSCVal();
}

public static class SCValConverter
{
    public static SCVal NativeToSCVal(object input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        Type type = input.GetType();

        // Handle primitive types
        if (type == typeof(int))
        {
            // Convert int to an unsigned 32-bit SCVal.
            // (Ensure your ints are non-negative or handle negatives as needed.)
            return new SCVal.ScvU32 { u32 = Convert.ToUInt32(input) };
        }
        else if (type == typeof(bool))
        {
            return new SCVal.ScvBool { b = (bool)input };
        }
        else if (type == typeof(string))
        {
            return new SCVal.ScvString { str = new SCString((string)input) };
        }
        // If the input is a map/dictionary, convert keys and values using native conversion.
        if (input is IDictionary dictionary)
        {
            var entries = new List<SCMapEntry>();
            
            
        }
        
        
        
        
        
        // Handle structs (non-primitive value types)
        if (type.IsValueType && !type.IsPrimitive)
        {
            // Use a dictionary to avoid duplicate keys when both a property and a field have the same name.
            var entryDict = new Dictionary<string, SCMapEntry>();

            // Process public properties
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(input);
                    var scVal = NativeToSCVal(value);
                    entryDict[prop.Name] = new SCMapEntry
                    {
                        key = new SCVal.ScvSymbol { sym = new SCSymbol(prop.Name) },
                        val = scVal
                    };
                }
            }

            // Process public fields (only if not already added via properties)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!entryDict.ContainsKey(field.Name))
                {
                    object value = field.GetValue(input);
                    SCVal scVal = NativeToSCVal(value);
                    entryDict[field.Name] = new SCMapEntry
                    {
                        key = new SCVal.ScvSymbol { sym = new SCSymbol(field.Name) },
                        val = scVal
                    };
                }
            }

            // Sort entries by the symbol (key) in alphabetical order
            var entries = entryDict.Values.ToList();
            entries.Sort((a, b) =>
                string.Compare(
                    ((SCVal.ScvSymbol)a.key).sym, 
                    ((SCVal.ScvSymbol)b.key).sym, 
                    StringComparison.Ordinal));
            // Create the SCMap and wrap it into an SCVal.ScvMap
            SCMap map = new SCMap(entries.ToArray());
            return new SCVal.ScvMap { map = map };
        }
        else
        {
            throw new NotSupportedException($"Type {type.Name} is not supported for conversion to SCVal.");
        }
    }
}

namespace ContractTypes
{
// ReSharper disable InconsistentNaming

    public struct SendInviteReq
    {
        public string host_address;
        public string guest_address;
        public int ledgers_until_expiration;
        public LobbyParameters parameters;
        
    }
    
    public struct LobbyParameters
    {
        public BoardDef board_def;
        public bool must_fill_all_tiles;
        public Dictionary<int, int> max_pawns;
        public bool dev_mode;
        public bool security_mode;
    }

    public struct BoardDef
    {
        public string name;
        public Pos size;
        public Dictionary<Pos, Tile> tiles;
        public bool is_hex;
        public Dictionary<int, int> default_max_pawns;

        public BoardDef(global::BoardDef boardDef)
        {
            name = boardDef.name;
            size = new Pos(boardDef.boardSize);
            tiles = new Dictionary<Pos, Tile>();
            foreach (global::Tile val in boardDef.tiles)
            {
                tiles[new Pos(val.pos)] = new Tile(val);
            }
            is_hex = boardDef.isHex;
            default_max_pawns = new Dictionary<int, int>();
            foreach (SMaxPawnsPerRank maxPawnsPerRank in boardDef.maxPawns)
            {
                default_max_pawns[(int)maxPawnsPerRank.rank] = maxPawnsPerRank.max;
            }
        }
    }
    
    public struct Tile
    {
        public Pos pos;
        public bool is_passable;
        public int setup_team;
        public int auto_setup_zone;

        public Tile(global::Tile tile)
        {
            pos = new Pos(tile.pos);
            is_passable = tile.isPassable;
            setup_team = (int)tile.setupTeam;
            auto_setup_zone = tile.autoSetupZone;
        }
    }
    
    public struct User
    {
        public string index;
        public string name;
        public int games_completed;
    }

    public struct Pos
    {
        public int x;
        public int y;

        public Pos(UnityEngine.Vector2Int vector)
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

    public struct FlatTestReq
    {
        public int number;
        public string word;
    }

    public struct NestedTestReq
    {
        public int numba;
        public string word;
        public FlatTestReq flat;
    }
    //
    // public struct Lobby
    // {
    //     public string lobby_id;
    //     public string host;
    //     public RBoardDef board_def;
    //     public bool must_fill_all_tiles;
    //     public Dictionary<int, int> max_pawns;
    //     public bool is_secure;
    //     public List<UserState> user_states;
    //     public int game_end_state;
    //     public List<Pawn> pawns;
    //
    //     public Lobby(string host_address, string guest_address, global::LobbyParameters parameters)
    //     {
    //         lobby_id = "UNDEFINED";
    //         host = host_address;
    //         board_def =  new RBoardDef(parameters.board);
    //         must_fill_all_tiles = parameters.mustFillAllTiles;
    //         max_pawns = new Dictionary<int, int>();
    //         for (int i = 0; i < parameters.maxPawns.Length; i++)
    //         {
    //             max_pawns[(int)parameters.maxPawns[i].rank] = parameters.maxPawns[i].max;
    //         }
    //         is_secure = false;
    //         user_states = new List<UserState>();
    //         // TODO: add user states
    //         game_end_state = 0;
    //         pawns = new List<Pawn>();
    //     }
    // }
    //

    // public struct RBoardDef
    // {
    //     public string name;
    //     public Vector2Int size;
    //     public Dictionary<Vector2Int, Tile> tiles;
    //     public bool isHex;
    //     public Dictionary<int, int> default_max_pawns;
    //
    //     public RBoardDef(BoardDef boardDef)
    //     {
    //         name = boardDef.boardName;
    //         size = new Vector2Int(boardDef.boardSize);
    //         tiles = new Dictionary<Vector2Int, Tile>();
    //         for (int i = 0; i < boardDef.tiles.Length; i++)
    //         {
    //             Tile tile = new(boardDef.tiles[i]);
    //             tiles[tile.pos] = tile;
    //         }
    //         isHex = boardDef.isHex;
    //         default_max_pawns = new Dictionary<int, int>();
    //         for (int i = 0; i < boardDef.maxPawns.Length; i++)
    //         {
    //             default_max_pawns[(int)boardDef.maxPawns[i].rank] = boardDef.maxPawns[i].max;
    //         }
    //     }
    // }


    // ReSharper restore InconsistentNaming

}

public enum NetworkStatus
{
    
}