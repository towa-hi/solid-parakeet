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
            "CDSL3FT6KO5CFQHQFGZHREBYR4HZEAJTMR2KJ2YYCF7QITXO4G7TKVL3");
        await client.TestFunction();
        
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

public interface XDRCompatable
{
    public SCVal ToSCVal();
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
        if (targetType == typeof(int))
        {
            // Prefer I32. If we get a U32, log a warning.
            if (scVal is SCVal.ScvI32 i32Val)
            {
                return i32Val.i32.InnerValue;
            }
            else if (scVal is SCVal.ScvU32 u32Val)
            {
                Debug.LogWarning("SCValToNative: Expected SCVal.ScvI32 for int conversion, got SCVal.ScvU32. Converting anyway.");
                return Convert.ToInt32(u32Val.u32.InnerValue);
            }
            else
            {
                throw new NotSupportedException("Expected SCVal.ScvI32 (or SCVal.ScvU32 as fallback) for int conversion.");
            }
        }
        else if (targetType == typeof(string))
        {
            if (scVal is SCVal.ScvString strVal)
                return strVal.str.InnerValue;
            else
                throw new NotSupportedException("Expected SCVal.ScvString for string conversion.");
        }
        else if (targetType == typeof(bool))
        {
            if (scVal is SCVal.ScvBool boolVal)
                return boolVal.b;
            else
                throw new NotSupportedException("Expected SCVal.ScvBool for bool conversion.");
        }
        // Special branch for dictionaries.
        else if (typeof(IDictionary).IsAssignableFrom(targetType))
        {
            if (scVal is SCVal.ScvMap scvMap)
            {
                var result = (IDictionary)Activator.CreateInstance(targetType);
                Type[] args = targetType.GetGenericArguments();
                Type keyType = args[0];
                Type valueType = args[1];
                foreach (SCMapEntry entry in scvMap.map.InnerValue)
                {
                    // We assume keys were converted to symbols.
                    string keyStr = ((SCVal.ScvSymbol)entry.key).sym.InnerValue;
                    object nativeKey;
                    if (keyType == typeof(int))
                        nativeKey = int.Parse(keyStr);
                    else if (keyType == typeof(string))
                        nativeKey = keyStr;
                    else
                        nativeKey = Convert.ChangeType(keyStr, keyType);
                    object nativeValue = SCValToNative(entry.val, valueType);
                    result.Add(nativeKey, nativeValue);
                }
                return result;
            }
            else
            {
                throw new NotSupportedException("Expected SCVal.ScvMap for dictionary conversion.");
            }
        }
        // Handle collections (arrays or IList) that are not dictionaries.
        else if (scVal is SCVal.ScvVec scvVec)
        {
            Type elementType = targetType.IsArray
                ? targetType.GetElementType()
                : (targetType.IsGenericType ? targetType.GetGenericArguments()[0] : typeof(object));
            if (elementType == null)
                throw new NotSupportedException("Unable to determine element type for collection conversion.");
            SCVal[] innerArray = scvVec.vec.InnerValue;
            int len = innerArray.Length;
            object[] convertedElements = new object[len];
            for (int i = 0; i < len; i++)
                convertedElements[i] = SCValToNative(innerArray[i], elementType);
            if (targetType.IsArray)
            {
                Array arr = Array.CreateInstance(elementType, len);
                for (int i = 0; i < len; i++)
                    arr.SetValue(convertedElements[i], i);
                return arr;
            }
            else
            {
                var list = (IList)Activator.CreateInstance(targetType);
                for (int i = 0; i < len; i++)
                    list.Add(convertedElements[i]);
                return list;
            }
        }
        // Handle structured types (native structs/classes) via SCVal.ScvMap.
        else if (scVal is SCVal.ScvMap scvMap2)
        {
            object instance = Activator.CreateInstance(targetType);
            var dict = new Dictionary<string, SCMapEntry>();
            foreach (SCMapEntry entry in scvMap2.map.InnerValue)
            {
                if (entry.key is SCVal.ScvSymbol sym)
                    dict[sym.sym.InnerValue] = entry;
                else
                    throw new NotSupportedException("Expected map key to be SCVal.ScvSymbol.");
            }
            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (dict.TryGetValue(field.Name, out SCMapEntry mapEntry))
                {
                    object fieldValue = SCValToNative(mapEntry.val, field.FieldType);
                    field.SetValue(instance, fieldValue);
                }
            }
            foreach (PropertyInfo prop in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanWrite) continue;
                if (dict.TryGetValue(prop.Name, out SCMapEntry mapEntry))
                {
                    object propValue = SCValToNative(mapEntry.val, prop.PropertyType);
                    prop.SetValue(instance, propValue);
                }
            }
            return instance;
        }
        else
        {
            throw new NotSupportedException("SCVal type not supported for conversion.");
        }
    }

    public static T SCValToNative<T>(SCVal scVal)
    {
        return (T)SCValToNative(scVal, typeof(T));
    }

    /// <summary>
    /// Converts a native object into an SCVal.
    /// For native ints, always produces an SCVal.ScvI32.
    /// </summary>
    public static SCVal NativeToSCVal(object input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        Type type = input.GetType();

        // Special handling for Pos: use I32 for its fields.
        if (type == typeof(Pos))
        {
            Pos pos = (Pos)input;
            var entries = new List<SCMapEntry>();
            entries.Add(new SCMapEntry
            {
                key = new SCVal.ScvSymbol { sym = new SCSymbol("x") },
                val = new SCVal.ScvI32 { i32 = new int32(pos.x) }
            });
            entries.Add(new SCMapEntry
            {
                key = new SCVal.ScvSymbol { sym = new SCSymbol("y") },
                val = new SCVal.ScvI32 { i32 = new int32(pos.y) }
            });
            entries.Sort((a, b) => string.Compare(((SCVal.ScvSymbol)a.key).sym.InnerValue,
                                                    ((SCVal.ScvSymbol)b.key).sym.InnerValue,
                                                    StringComparison.Ordinal));
            return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
        }
        // For native int (outside Pos), always convert to SCVal.ScvI32.
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
        // Handle dictionaries.
        else if (input is IDictionary dict)
        {
            var entries = new List<SCMapEntry>();
            foreach (DictionaryEntry entry in dict)
            {
                string keyStr = entry.Key.ToString();
                SCVal keySCVal = new SCVal.ScvSymbol { sym = new SCSymbol(keyStr) };
                SCVal valueSCVal = NativeToSCVal(entry.Value);
                entries.Add(new SCMapEntry { key = keySCVal, val = valueSCVal });
            }
            entries.Sort((a, b) => string.Compare(((SCVal.ScvSymbol)a.key).sym.InnerValue,
                                                    ((SCVal.ScvSymbol)b.key).sym.InnerValue,
                                                    StringComparison.Ordinal));
            return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
        }
        // Handle collections (arrays or IList) that are not dictionaries.
        else if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            var elements = new List<SCVal>();
            foreach (var item in (IEnumerable)input)
                elements.Add(NativeToSCVal(item));
            return new SCVal.ScvVec { vec = new SCVec(elements.ToArray()) };
        }
        // Otherwise, assume a structured type.
        else
        {
            var entries = new List<SCMapEntry>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object fieldValue = field.GetValue(input);
                SCVal scFieldVal = NativeToSCVal(fieldValue);
                entries.Add(new SCMapEntry
                {
                    key = new SCVal.ScvSymbol { sym = new SCSymbol(field.Name) },
                    val = scFieldVal
                });
            }
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead) continue;
                object propValue = prop.GetValue(input);
                SCVal scPropVal = NativeToSCVal(propValue);
                entries.Add(new SCMapEntry
                {
                    key = new SCVal.ScvSymbol { sym = new SCSymbol(prop.Name) },
                    val = scPropVal
                });
            }
            // Todo: make a more consistent sort that follows this https://github.com/stellar/js-stellar-base/blob/e77bb26492adc6d4a886324cedd6781556af67da/src/scval.js#L191
            entries.Sort((a, b) => string.Compare(((SCVal.ScvSymbol)a.key).sym.InnerValue,
                                                    ((SCVal.ScvSymbol)b.key).sym.InnerValue,
                                                    StringComparison.Ordinal));
            return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
        }
    }

    /// <summary>
    /// Deeply compares two SCVal objects for equality.
    /// Logs debug warnings when mismatches are found.
    /// Special handling is added so that SCVal.ScvI32 and SCVal.ScvU32 are considered equal
    /// if their numeric values match.
    /// </summary>
    public static bool DeepEqual(SCVal a, SCVal b)
    {
        if (a == null || b == null)
        {
            if (a != b)
                Debug.LogWarning("DeepEqual: One value is null while the other is not.");
            return a == b;
        }

        // Special handling: if one is I32 and the other is U32, compare numeric values.
        if ((a.Discriminator == SCValType.SCV_I32 && b.Discriminator == SCValType.SCV_U32) ||
            (a.Discriminator == SCValType.SCV_U32 && b.Discriminator == SCValType.SCV_I32))
        {
            int aVal = (a is SCVal.ScvI32) ? ((SCVal.ScvI32)a).i32.InnerValue : Convert.ToInt32(((SCVal.ScvU32)a).u32.InnerValue);
            int bVal = (b is SCVal.ScvI32) ? ((SCVal.ScvI32)b).i32.InnerValue : Convert.ToInt32(((SCVal.ScvU32)b).u32.InnerValue);
            if (aVal != bVal)
            {
                Debug.LogWarning($"DeepEqual: Int value mismatch: {aVal} vs {bVal}");
                return false;
            }
            return true;
        }

        if (a.Discriminator != b.Discriminator)
        {
            Debug.LogWarning($"DeepEqual: Discriminator mismatch: {a.Discriminator} vs {b.Discriminator}");
            return false;
        }

        switch (a.Discriminator)
        {
            case SCValType.SCV_I32:
                {
                    var aVal = ((SCVal.ScvI32)a).i32.InnerValue;
                    var bVal = ((SCVal.ScvI32)b).i32.InnerValue;
                    if (!aVal.Equals(bVal))
                    {
                        Debug.LogWarning($"DeepEqual: I32 mismatch: {aVal} vs {bVal}");
                        return false;
                    }
                    return true;
                }
            case SCValType.SCV_U32:
                {
                    var aVal = ((SCVal.ScvU32)a).u32.InnerValue;
                    var bVal = ((SCVal.ScvU32)b).u32.InnerValue;
                    if (!aVal.Equals(bVal))
                    {
                        Debug.LogWarning($"DeepEqual: U32 mismatch: {aVal} vs {bVal}");
                        return false;
                    }
                    return true;
                }
            case SCValType.SCV_STRING:
                {
                    var aVal = ((SCVal.ScvString)a).str.InnerValue;
                    var bVal = ((SCVal.ScvString)b).str.InnerValue;
                    if (!aVal.Equals(bVal))
                    {
                        Debug.LogWarning($"DeepEqual: String mismatch: '{aVal}' vs '{bVal}'");
                        return false;
                    }
                    return true;
                }
            case SCValType.SCV_BOOL:
                {
                    var aVal = ((SCVal.ScvBool)a).b;
                    var bVal = ((SCVal.ScvBool)b).b;
                    if (!aVal.Equals(bVal))
                    {
                        Debug.LogWarning($"DeepEqual: Bool mismatch: {aVal} vs {bVal}");
                        return false;
                    }
                    return true;
                }
            case SCValType.SCV_VEC:
                {
                    var vecA = ((SCVal.ScvVec)a).vec.InnerValue;
                    var vecB = ((SCVal.ScvVec)b).vec.InnerValue;
                    if (vecA.Length != vecB.Length)
                    {
                        Debug.LogWarning($"DeepEqual: Vector length mismatch: {vecA.Length} vs {vecB.Length}");
                        return false;
                    }
                    for (int i = 0; i < vecA.Length; i++)
                    {
                        if (!DeepEqual(vecA[i], vecB[i]))
                        {
                            Debug.LogWarning($"DeepEqual: Vector element at index {i} mismatch.");
                            return false;
                        }
                    }
                    return true;
                }
            case SCValType.SCV_MAP:
                {
                    var mapA = ((SCVal.ScvMap)a).map.InnerValue;
                    var mapB = ((SCVal.ScvMap)b).map.InnerValue;
                    if (mapA.Length != mapB.Length)
                    {
                        Debug.LogWarning($"DeepEqual: Map entry count mismatch: {mapA.Length} vs {mapB.Length}");
                        return false;
                    }
                    var sortedA = mapA.OrderBy(e => ((SCVal.ScvSymbol)e.key).sym.InnerValue).ToArray();
                    var sortedB = mapB.OrderBy(e => ((SCVal.ScvSymbol)e.key).sym.InnerValue).ToArray();
                    for (int i = 0; i < sortedA.Length; i++)
                    {
                        var keyA = ((SCVal.ScvSymbol)sortedA[i].key).sym.InnerValue;
                        var keyB = ((SCVal.ScvSymbol)sortedB[i].key).sym.InnerValue;
                        if (!keyA.Equals(keyB))
                        {
                            Debug.LogWarning($"DeepEqual: Map key mismatch at index {i}: '{keyA}' vs '{keyB}'");
                            return false;
                        }
                        if (!DeepEqual(sortedA[i].val, sortedB[i].val))
                        {
                            Debug.LogWarning($"DeepEqual: Map value mismatch for key '{keyA}'");
                            return false;
                        }
                    }
                    return true;
                }
            default:
                Debug.LogWarning("DeepEqual: Unsupported SCVal type for deep equality check.");
                return a.Equals(b);
        }
    }

    /// <summary>
    /// Test: Converts a SendInviteReq SCVal to a native object and back,
    /// then checks deep equality.
    /// </summary>
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
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("1") },
                    val = new SCVal.ScvI32() { i32 = new int32(42) }
                }
            })
        };

        // --- Build BoardDef ---
        SCVal.ScvMap boardDefMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
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
                // is_hex
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("is_hex") },
                    val = new SCVal.ScvBool() { b = true }
                },
                // default_max_pawns
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("default_max_pawns") },
                    val = maxPawnsMap
                }
            })
        };

        // --- Build LobbyParameters ---
        SCVal.ScvMap lobbyParamsMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                // board_def
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("board_def") },
                    val = boardDefMap
                },
                // must_fill_all_tiles
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("must_fill_all_tiles") },
                    val = new SCVal.ScvBool() { b = true }
                },
                // max_pawns
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("max_pawns") },
                    val = maxPawnsMap
                },
                // dev_mode
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("dev_mode") },
                    val = new SCVal.ScvBool() { b = false }
                },
                // security_mode
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("security_mode") },
                    val = new SCVal.ScvBool() { b = true }
                }
            })
        };

        // --- Build SendInviteReq ---
        SCVal.ScvMap sendInviteReqMap = new SCVal.ScvMap()
        {
            map = new SCMap(new SCMapEntry[]
            {
                // guest_address
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("guest_address") },
                    val = new SCVal.ScvString() { str = new SCString("Guest123") }
                },
                // host_address
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("host_address") },
                    val = new SCVal.ScvString() { str = new SCString("Host456") }
                },
                // ledgers_until_expiration
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("ledgers_until_expiration") },
                    val = new SCVal.ScvI32() { i32 = new int32(10) }
                },
                // parameters
                new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol("parameters") },
                    val = lobbyParamsMap
                }
            })
        };

        // Convert the original SCVal into a native SendInviteReq.
        SendInviteReq inviteReq = SCValToNative<SendInviteReq>(sendInviteReqMap);

        // Convert the native object back into an SCVal.
        SCVal roundTrip = NativeToSCVal(inviteReq);

        // Check deep equality.
        bool areEqual = DeepEqual(sendInviteReqMap, roundTrip);
        Debug.Log($"Roundtrip equality: {areEqual}");
        return roundTrip;
        
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
        public int number;
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
