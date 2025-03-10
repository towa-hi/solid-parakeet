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
        SCVal.ScvMap flatTestReqMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("number") },
                    val = new SCVal.ScvI32() { i32 = 42 }  // Example value
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("word") },
                    val = new SCVal.ScvString() { str = new SCString("hello") }  // Example value
                },
            }),
        };
        
        // Then, create the parent NestedTestReq struct as an SCMap
        SCVal.ScvMap nestedTestReqMap = new SCVal.ScvMap()
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
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("number") },
                    val = new SCVal.ScvI32() { i32 = 100 }  // Example value
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("word") },
                    val = new SCVal.ScvString() { str = new SCString("world") }  // Example value
                },
        
            }),
        };
        
        NestedTestReq test = SCValConverter.SCValToNative<NestedTestReq>(nestedTestReqMap);
        
        // StellarDotnet stellar = new StellarDotnet("SBBAF3LZZPQVPPBJKSY2ZE7EF2L3IIWRL7RXQCXVOELS4NQRMNLZN6PB", "CBTBFRIT5GIMIFLI6WWVHSJA7VWRI2TGX32VACQQO5W53UWVZ674Q4OB");
        // await stellar.TestFunction();
        return true;
    }


    public async Task<bool> SecondTestFunction(string guestAddress)
    {
        SCVal data = SCValConverter.TestSendInviteReqRoundTripConversion();
        StellarDotnet client = new StellarDotnet(
            "SBBAF3LZZPQVPPBJKSY2ZE7EF2L3IIWRL7RXQCXVOELS4NQRMNLZN6PB",
            "CDB6HCKZCRDFCYPTNYX522BB3VPQ3SQ276VNI6TPO5LJE64GVX77G2MA");
        
        
        await client.TestFunction(data);
        
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

public class SendInviteTestReq
{
    public string host_address;
    public string guest_address;
    public int ledgers_until_expiration;
}

public static class SCValConverter
{
    /// <summary>
    /// Converts an SCVal into a native object of the given type.
    /// For ints, we now expect and produce SCVal.ScvI32. 
    /// If a SCVal.ScvU32 is encountered when expecting an int, a warning is logged.
    /// </summary>
    public static object SCValToNative(SCVal scVal, Type targetType)
    {
        Debug.Log($"SCValToNative: Converting SCVal of discriminator {scVal.Discriminator} to native type {targetType}.");
        if (targetType == typeof(int))
        {
            Debug.Log("SCValToNative: Target type is int.");
            // Prefer I32. If we get a U32, log a warning.
            if (scVal is SCVal.ScvI32 i32Val)
            {
                Debug.Log($"SCValToNative: Found SCVal.ScvI32 with value {i32Val.i32.InnerValue}.");
                return i32Val.i32.InnerValue;
            }
            else if (scVal is SCVal.ScvU32 u32Val)
            {
                Debug.LogWarning("SCValToNative: Expected SCVal.ScvI32 for int conversion, got SCVal.ScvU32. Converting anyway.");
                return Convert.ToInt32(u32Val.u32.InnerValue);
            }
            else
            {
                Debug.LogError("SCValToNative: Failed int conversion. SCVal is not I32 or U32.");
                throw new NotSupportedException("Expected SCVal.ScvI32 (or SCVal.ScvU32 as fallback) for int conversion.");
            }
        }
        else if (targetType == typeof(string))
        {
            Debug.Log("SCValToNative: Target type is string.");
            if (scVal is SCVal.ScvString strVal)
            {
                Debug.Log($"SCValToNative: Found SCVal.ScvString with value '{strVal.str.InnerValue}'.");
                return strVal.str.InnerValue;
            }
            else
            {
                Debug.LogError("SCValToNative: Failed string conversion. SCVal is not SCvString.");
                throw new NotSupportedException("Expected SCVal.ScvString for string conversion.");
            }
        }
        else if (targetType == typeof(bool))
        {
            Debug.Log("SCValToNative: Target type is bool.");
            if (scVal is SCVal.ScvBool boolVal)
            {
                Debug.Log($"SCValToNative: Found SCVal.ScvBool with value {boolVal.b}.");
                return boolVal.b;
            }
            else
            {
                Debug.LogError("SCValToNative: Failed bool conversion. SCVal is not SCvBool.");
                throw new NotSupportedException("Expected SCVal.ScvBool for bool conversion.");
            }
        }
        else if (scVal is SCVal.ScvVec scvVec)
        {
            Debug.Log("SCValToNative: Target type is a collection. Using vector conversion branch.");
            Type elementType = targetType.IsArray
                ? targetType.GetElementType()
                : (targetType.IsGenericType ? targetType.GetGenericArguments()[0] : typeof(object));
            if (elementType == null)
            {
                Debug.LogError("SCValToNative: Unable to determine element type for collection conversion.");
                throw new NotSupportedException("Unable to determine element type for collection conversion.");
            }
            SCVal[] innerArray = scvVec.vec.InnerValue;
            int len = innerArray.Length;
            object[] convertedElements = new object[len];
            for (int i = 0; i < len; i++)
            {
                Debug.Log($"SCValToNative: Converting collection element at index {i}.");
                convertedElements[i] = SCValToNative(innerArray[i], elementType);
            }
            if (targetType.IsArray)
            {
                Array arr = Array.CreateInstance(elementType, len);
                for (int i = 0; i < len; i++)
                {
                    arr.SetValue(convertedElements[i], i);
                }
                Debug.Log("SCValToNative: Collection converted to array.");
                return arr;
            }
        }
        // Handle structured types (native structs/classes) via SCVal.ScvMap.
        else if (scVal is SCVal.ScvMap scvMap)
        {
            Debug.Log("SCValToNative: Target type is either a map or a structured type.");
            // sorting test
            SCMapEntry[] originalEntries = scvMap.map.InnerValue;
            SCMapEntry[] sortedEntries = (SCMapEntry[])originalEntries.Clone();
            Array.Sort(sortedEntries, new SCMapEntryComparer());
            bool orderChanged = false;
            for (int i = 0; i < sortedEntries.Length; i++)
            {
                int cmp = new SCMapEntryComparer().Compare(originalEntries[i], sortedEntries[i]);
                if (cmp != 0)
                {
                    orderChanged = true;
                    Debug.LogWarning($"SCValToNative: got a map with invalid entry order!!!! index {i}");
                    break;
                }
            }
            // if is a OrderedDictionary
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(OrderedDictionary<,>))
            {
                object instance = Activator.CreateInstance(targetType);
                IDictionary dict = (IDictionary)instance;
                Type[] args = targetType.GetGenericArguments();
                Type keyType = args[0];
                Type valueType = args[1];
                foreach (SCMapEntry entry in sortedEntries)
                {
                    object nativeKey = SCValToNative(entry.key, keyType);
                    object nativeValue = SCValToNative(entry.val, valueType);
                    Debug.Log($"SCValToNative: Adding dictionary entry with key '{nativeKey}' and value '{nativeValue}'.");
                    dict.Add(nativeKey, nativeValue);
                }
                return instance;
            }
            // if is a struct
            if (targetType.IsValueType && !targetType.IsPrimitive)
            {
                object instance = Activator.CreateInstance(targetType);
                Debug.Log("SCValToNative: Target type is a struct");
                Dictionary<string, SCMapEntry> dict = new Dictionary<string, SCMapEntry>();
                foreach (SCMapEntry entry in scvMap.map.InnerValue)
                {
                    if (entry.key is SCVal.ScvSymbol sym)
                    {
                        dict[sym.sym.InnerValue] = entry;
                        Debug.Log($"SCValToNative: Found map key '{sym.sym.InnerValue}'.");
                    }
                    else
                    {
                        Debug.LogError("SCValToNative: Expected map key to be SCVal.ScvSymbol.");
                        throw new NotSupportedException("Expected map key to be SCVal.ScvSymbol.");
                    }
                }
                foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (dict.TryGetValue(field.Name, out SCMapEntry mapEntry))
                    {
                        Debug.Log($"SCValToNative: Converting field '{field.Name}'.");
                        object fieldValue = SCValToNative(mapEntry.val, field.FieldType);
                        field.SetValue(instance, fieldValue);
                    }
                    else
                    {
                        Debug.LogWarning($"SCValToNative: Field '{field.Name}' not found in SCVal map.");
                    }
                }
                return instance;
            }
        }
        Debug.LogError("SCValToNative: SCVal type not supported for conversion.");
        throw new NotSupportedException("SCVal type not supported for conversion.");
    }


    public static T SCValToNative<T>(SCVal scVal)
    {
        return (T)SCValToNative(scVal, typeof(T));
    }

    public static SCVal NativeToSCVal(object input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        Type type = input.GetType();
        // For native int always convert to SCVal.ScvI32.
        if (type == typeof(int))
        {
            return new SCVal.ScvI32 { i32 = new int32((int)input) };
        }
        else if (type == typeof(string))
        {
            return new SCVal.ScvString { str = new SCString((string)input) };
        }
        else if (type == typeof(bool))
        {
            return new SCVal.ScvBool { b = (bool)input };
        }
        // Handle OrderedDictionaries.
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OrderedDictionary<,>))
        {
            IDictionary dict = (IDictionary)input;
            List<SCMapEntry> entries = new List<SCMapEntry>();
            foreach (DictionaryEntry entry in dict)
            {
                SCVal keySCVal = NativeToSCVal(entry.Key);
                SCVal valueSCVal = NativeToSCVal(entry.Value);
                entries.Add(new SCMapEntry { key = keySCVal, val = valueSCVal });
            }
            // Sort using our custom comparer.
            entries.Sort(new SCMapEntryComparer());
            return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
        }
        // Handle collections (arrays or IList) that are not dictionaries.
        else if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            List<SCVal> elements = new List<SCVal>();
            foreach (object item in (IEnumerable)input)
            {
                elements.Add(NativeToSCVal(item));
            }
            return new SCVal.ScvVec { vec = new SCVec(elements.ToArray()) };
        }
        // Otherwise, assume a structured type.
        else
        {
            List<SCMapEntry> entries = new List<SCMapEntry>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object fieldValue = field.GetValue(input);
                SCVal scFieldVal = NativeToSCVal(fieldValue);
                entries.Add(new SCMapEntry
                {
                    key = new SCVal.ScvSymbol { sym = new SCSymbol(field.Name) },
                    val = scFieldVal,
                });
            }
            entries.Sort(new SCMapEntryComparer());
            return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
        }
    }


    public static bool HashEqual(SCVal a, SCVal b)
    {
        string encodedA = SCValXdr.EncodeToBase64(a);
        Debug.Log(encodedA);
        string encodedB = SCValXdr.EncodeToBase64(b);
        Debug.Log(encodedB);
        return encodedA == encodedB;
    }
    
    public static SCVal TestSendInviteReqRoundTripConversion()
    {
        // --- Build Pos as a structured type (with fields "x" and "y") ---
        SCVal.ScvMap posMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("x") },
                    val = new SCVal.ScvI32() { i32 = new int32(10) }
                },
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("y") },
                    val = new SCVal.ScvI32() { i32 = new int32(20) }
                }
            })
        };

        // --- Build an empty tiles map for BoardDef ---
        SCVal.ScvMap tilesMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[0])
        };

        // --- Build default_max_pawns as a map: key "1" -> 42 ---
        SCVal.ScvMap maxPawnsMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                new SCMapEntry()
                {
                    key = new SCVal.ScvI32() { i32 = new int32(1447) },
                    val = new SCVal.ScvI32() { i32 = new int32(42) }
                }
            })
        };
        SCMapEntryComparer.SortMap(maxPawnsMap);
        // --- Build BoardDef ---
        SCVal.ScvMap boardDefMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                // default_max_pawns
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("default_max_pawns") },
                    val = maxPawnsMap
                },
                // is_hex
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("is_hex") },
                    val = new SCVal.ScvBool() { b = true }
                },
                // name
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("name") },
                    val = new SCVal.ScvString() { str = new SCString("TestBoard") }
                },
                // size (a Pos)
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("size") },
                    val = posMap
                },
                // tiles
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("tiles") },
                    val = tilesMap
                },
            }),
        };
        SCMapEntryComparer.SortMap(boardDefMap);
        // --- Build LobbyParameters ---
        SCVal.ScvMap lobbyParamsMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                // board_def
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("board_def") },
                    val = boardDefMap,
                },
                // dev_mode
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("dev_mode") },
                    val = new SCVal.ScvBool() { b = false },
                },
                // must_fill_all_tiles
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("must_fill_all_tiles") },
                    val = new SCVal.ScvBool() { b = true },
                },
                // max_pawns
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("max_pawns") },
                    val = maxPawnsMap,
                },
                // security_mode
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("security_mode") },
                    val = new SCVal.ScvBool() { b = true },
                },
            })
        };
        SCMapEntryComparer.SortMap(lobbyParamsMap);
        // --- Build SendInviteReq ---
        SCVal.ScvMap sendInviteReqMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                // guest_address
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("guest_address") },
                    val = new SCVal.ScvString() { str = new SCString("Guest123") },
                },
                // host_address
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("host_address") },
                    val = new SCVal.ScvString() { str = new SCString("GDB62OX5R73Y7WTTSZBCXKYXCP2MYJGTEAOAMNORJI3WQQWT2AMMTR24") },
                },
                // ledgers_until_expiration
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("ledgers_until_expiration") },
                    val = new SCVal.ScvI32() { i32 = new int32(10) },
                },
                // parameters
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("parameters") },
                    val = lobbyParamsMap,
                },
            }),
        };
        SCMapEntryComparer.SortMap(sendInviteReqMap);
        // Convert the original SCVal into a native SendInviteReq.

        SendInviteReq inviteReq = SCValToNative<SendInviteReq>(sendInviteReqMap);

        // Convert the native object back into an SCVal.
        SCVal roundTrip = NativeToSCVal(inviteReq);

        bool areEqual = HashEqual(sendInviteReqMap, roundTrip);
        Debug.Log(areEqual);
        // // Check deep equality.
        // bool areEqual = DeepEqual(sendInviteReqMap, roundTrip);
        // Debug.Log($"Roundtrip equality: {areEqual}");
        // return roundTrip;
        
        return sendInviteReqMap;
        
        
    }
}

public class SCMapEntryComparer : IComparer<SCMapEntry>
{
    public int Compare(SCMapEntry x, SCMapEntry y)
    {
        return SCValComparer.Compare(x.key, y.key);
    }

    public static void SortMap(SCVal.ScvMap scvMap)
    {
        List<SCMapEntry> entries = new List<SCMapEntry>(scvMap.map.InnerValue);
        entries.Sort(new SCMapEntryComparer());
        scvMap.map.InnerValue = entries.ToArray();
    }
}

public static class SCValComparer
{
    public static int Compare(SCVal a, SCVal b)
    {
        // First, compare the discriminators (tags) numerically.
        if (a.Discriminator != b.Discriminator)
        {
            return ((int)a.Discriminator).CompareTo((int)b.Discriminator);
        }

        // Same tag: compare based on type.
        switch (a.Discriminator)
        {
            case SCValType.SCV_I32:
            {
                int aVal = ((SCVal.ScvI32)a).i32.InnerValue;
                int bVal = ((SCVal.ScvI32)b).i32.InnerValue;
                return aVal.CompareTo(bVal);
            }
            case SCValType.SCV_U32:
            {
                uint aVal = ((SCVal.ScvU32)a).u32.InnerValue;
                uint bVal = ((SCVal.ScvU32)b).u32.InnerValue;
                return aVal.CompareTo(bVal);
            }
            case SCValType.SCV_STRING:
            {
                string aVal = ((SCVal.ScvString)a).str.InnerValue;
                string bVal = ((SCVal.ScvString)b).str.InnerValue;
                return string.Compare(aVal, bVal, StringComparison.Ordinal);
            }
            case SCValType.SCV_BOOL:
            {
                bool aVal = ((SCVal.ScvBool)a).b;
                bool bVal = ((SCVal.ScvBool)b).b;
                return aVal.CompareTo(bVal); // false < true.
            }
            case SCValType.SCV_SYMBOL:
            {
                string aVal = ((SCVal.ScvSymbol)a).sym.InnerValue;
                string bVal = ((SCVal.ScvSymbol)b).sym.InnerValue;
                return string.Compare(aVal, bVal, StringComparison.Ordinal);
            }
            case SCValType.SCV_MAP:
            {
                // TODO: figure out how map comparison works
                return a.ToString().CompareTo(b.ToString());
            }
            default:
            {
                // For other small-value types, fall back to comparing their string representations.
                return a.ToString().CompareTo(b.ToString());
            }
        }
    }
}

public interface IScvMapCompatable
{
    SCVal.ScvMap ToScvMap();
}

namespace ContractTypes
{
// ReSharper disable InconsistentNaming

    public struct SendInviteReq: IScvMapCompatable
    {
        public string host_address;
        public string guest_address;
        public int ledgers_until_expiration;
        public LobbyParameters parameters;

        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    // guest_address
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("guest_address") },
                        val = new SCVal.ScvString() { str = new SCString(guest_address) },
                    },
                    // host_address
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("host_address") },
                        val = new SCVal.ScvString() { str = new SCString(host_address) },
                    },
                    // ledgers_until_expiration
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("ledgers_until_expiration") },
                        val = new SCVal.ScvI32() { i32 = new int32(ledgers_until_expiration) },
                    },
                    // parameters
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("parameters") },
                        val = parameters.ToScvMap(),
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct LobbyParameters: IScvMapCompatable
    {
        public BoardDef board_def;
        public bool dev_mode;
        public OrderedDictionary<int, int> max_pawns;
        public bool must_fill_all_tiles;
        public bool security_mode;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    // board_def
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("board_def") },
                        val = board_def.ToScvMap(),
                    },
                    // dev_mode
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("dev_mode") },
                        val = new SCVal.ScvBool() { b = dev_mode },
                    },
                    // must_fill_all_tiles
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("must_fill_all_tiles") },
                        val = new SCVal.ScvBool() { b = must_fill_all_tiles },
                    },
                    // max_pawns
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("max_pawns") },
                        val = max_pawns.ToScvMap(),
                    },
                    // security_mode
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("security_mode") },
                        val = new SCVal.ScvBool() { b = security_mode },
                    },
                })
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }

    public struct BoardDef: IScvMapCompatable
    {
        public OrderedDictionary<int, int> default_max_pawns;
        public bool is_hex;
        public string name;
        public Pos size;
        public OrderedDictionary<Pos, Tile> tiles;

        public BoardDef(global::BoardDef boardDef)
        {
            name = boardDef.name;
            size = new Pos(boardDef.boardSize);
            tiles = new OrderedDictionary<Pos, Tile>();
            foreach (global::Tile val in boardDef.tiles)
            {
                tiles.Add(new Pos(val.pos), new Tile(val));
            }
            is_hex = boardDef.isHex;
            default_max_pawns = new OrderedDictionary<int, int>();
            foreach (SMaxPawnsPerRank maxPawnsPerRank in boardDef.maxPawns)
            {
                default_max_pawns.Add((int)maxPawnsPerRank.rank, maxPawnsPerRank.max);
            }
        }

        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    // default_max_pawns
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("default_max_pawns") },
                        val = default_max_pawns.ToScvMap(),
                    },
                    // is_hex
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("is_hex") },
                        val = new SCVal.ScvBool() { b = is_hex }
                    },
                    // name
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("name") },
                        val = new SCVal.ScvString() { str = new SCString(name) }
                    },
                    // size (a Pos)
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("size") },
                        val = size.ToScvMap(),
                    },
                    // tiles
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("tiles") },
                        val = tiles.ToScvMap(),
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct Tile: IScvMapCompatable
    {
        public int auto_setup_zone;
        public bool is_passable;
        public Pos pos;
        public int setup_team;

        public Tile(global::Tile tile)
        {
            pos = new Pos(tile.pos);
            is_passable = tile.isPassable;
            setup_team = (int)tile.setupTeam;
            auto_setup_zone = tile.autoSetupZone;
        }

        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("auto_setup_zone") },
                        val = new SCVal.ScvI32() { i32 = new int32(auto_setup_zone) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("is_passable") },
                        val = new SCVal.ScvBool() { b = is_passable },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("pos") },
                        val = pos.ToScvMap(),
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("setup_team") },
                        val = new SCVal.ScvI32() { i32 = new int32(setup_team) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct User: IScvMapCompatable
    {
        public int games_completed;
        public string index;
        public string name;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("games_completed") },
                        val = new SCVal.ScvI32() { i32 = new int32(games_completed) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("index") },
                        val = new SCVal.ScvString() { str = new SCString(index) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("name") },
                        val = new SCVal.ScvString() { str = new SCString(name) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }

    public struct Pos: IScvMapCompatable
    {
        public int x;
        public int y;

        public Pos(UnityEngine.Vector2Int vector)
        {
            x = vector.x;
            y = vector.y;
        }

        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("x") },
                        val = new SCVal.ScvI32() { i32 = new int32(x) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("y") },
                        val = new SCVal.ScvI32() { i32 = new int32(y) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct UserState: IScvMapCompatable
    {
        public int team;
        public string user_id;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("team") },
                        val = new SCVal.ScvI32() { i32 = new int32(team) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("user_id") },
                        val = new SCVal.ScvString() { str = new SCString(user_id) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }

    public struct PawnDef: IScvMapCompatable
    {
        public string def_id;
        public int movement_range;
        public string name;
        public int power;
        public int rank;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("def_id") },
                        val = new SCVal.ScvString() { str = new SCString(def_id) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("movement_range") },
                        val = new SCVal.ScvI32() { i32 = new int32(movement_range) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("name") },
                        val = new SCVal.ScvString() { str = new SCString(name) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("power") },
                        val = new SCVal.ScvI32() { i32 = new int32(power) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("rank") },
                        val = new SCVal.ScvI32() { i32 = new int32(rank) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    
    public struct PawnCommitment: IScvMapCompatable
    {
        string def_hidden;
        string pawn_id;
        Pos pos;
        string user_id;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("def_hidden") },
                        val = new SCVal.ScvString() { str = new SCString(def_hidden) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("pawn_id") },
                        val = new SCVal.ScvString() { str = new SCString(pawn_id) },
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("pos") },
                        val = pos.ToScvMap(),
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("user_id") },
                        val = new SCVal.ScvString() { str = new SCString(user_id) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct SetupCommitment: IScvMapCompatable
    {
        List<PawnCommitment> pawn_positions;
        string user_id;
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("pawn_positions") },
                        val = SCValConverter.NativeToSCVal(pawn_positions),
                    },
                    new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol("user_id") },
                        val = new SCVal.ScvString() { str = new SCString(user_id) },
                    },
                }),
            };
            SCMapEntryComparer.SortMap(scvMap);
            return scvMap;
        }
    }
    
    public struct FlatTestReq: IScvMapCompatable
    {
        public int number;
        public string word;
        public SCVal.ScvMap ToScvMap()
        {
            throw new NotImplementedException();
        }
    }

    public struct NestedTestReq: IScvMapCompatable
    {
        public FlatTestReq flat;
        public int number;
        public string word;
        public SCVal.ScvMap ToScvMap()
        {
            throw new NotImplementedException();
        }
    }
}


public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IScvMapCompatable
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly List<TKey> _keys;

    public OrderedDictionary()
    {
        _dictionary = new Dictionary<TKey, TValue>();
        _keys = new List<TKey>();
    }

    // IDictionary<TKey, TValue> members
    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set
        {
            if (!_dictionary.ContainsKey(key))
                _keys.Add(key);
            _dictionary[key] = value;
        }
    }

    public ICollection<TKey> Keys => _keys.AsReadOnly();

    public ICollection<TValue> Values
    {
        get
        {
            List<TValue> values = new List<TValue>(_keys.Count);
            foreach (TKey key in _keys)
                values.Add(_dictionary[key]);
            return values.AsReadOnly();
        }
    }

    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        if (_dictionary.ContainsKey(key))
            throw new ArgumentException("An element with the same key already exists.");
        _dictionary.Add(key, value);
        _keys.Add(key);
    }

    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (_dictionary.Remove(key))
        {
            _keys.Remove(key);
            return true;
        }
        return false;
    }

    public bool TryGetValue(TKey key, out TValue value) =>
        _dictionary.TryGetValue(key, out value);

    public void Add(KeyValuePair<TKey, TValue> item) =>
        Add(item.Key, item.Value);

    public void Clear()
    {
        _dictionary.Clear();
        _keys.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        _dictionary.ContainsKey(item.Key) &&
        EqualityComparer<TValue>.Default.Equals(_dictionary[item.Key], item.Value);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach (TKey key in _keys)
        {
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Contains(item))
            return Remove(item.Key);
        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (TKey key in _keys)
        {
            yield return new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary members (non-generic)
    object IDictionary.this[object key]
    {
        get
        {
            if (key is TKey typedKey)
                return this[typedKey];
            throw new ArgumentException("Key is of an incorrect type");
        }
        set
        {
            if (key is TKey typedKey && value is TValue typedValue)
                this[typedKey] = typedValue;
            else
                throw new ArgumentException("Key or value is of an incorrect type");
        }
    }

    bool IDictionary.IsFixedSize => false;

    ICollection IDictionary.Keys => _keys;

    ICollection IDictionary.Values
    {
        get
        {
            List<TValue> values = new List<TValue>(_keys.Count);
            foreach (TKey key in _keys)
                values.Add(_dictionary[key]);
            return values;
        }
    }

    void IDictionary.Add(object key, object value)
    {
        if (key is TKey typedKey && value is TValue typedValue)
            Add(typedKey, typedValue);
        else
            throw new ArgumentException("Key or value is of an incorrect type");
    }

    bool IDictionary.Contains(object key)
    {
        if (key is TKey typedKey)
            return ContainsKey(typedKey);
        return false;
    }

    IDictionaryEnumerator IDictionary.GetEnumerator()
    {
        return new OrderedDictionaryEnumerator(this);
    }

    void IDictionary.Remove(object key)
    {
        if (key is TKey typedKey)
            Remove(typedKey);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        foreach (var pair in this)
        {
            array.SetValue(pair, index++);
        }
    }

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    private class OrderedDictionaryEnumerator : IDictionaryEnumerator
    {
        private readonly IEnumerator<KeyValuePair<TKey, TValue>> _enumerator;

        public OrderedDictionaryEnumerator(OrderedDictionary<TKey, TValue> dict)
        {
            _enumerator = dict.GetEnumerator();
        }

        public DictionaryEntry Entry => new DictionaryEntry(Key, Value);

        public object Key => _enumerator.Current.Key;

        public object Value => _enumerator.Current.Value;

        public object Current => Entry;

        public bool MoveNext() => _enumerator.MoveNext();

        public void Reset() => _enumerator.Reset();
    }

    public SCVal.ScvMap ToScvMap()
    {
        return (SCVal.ScvMap)SCValConverter.NativeToSCVal(this);
    }
}