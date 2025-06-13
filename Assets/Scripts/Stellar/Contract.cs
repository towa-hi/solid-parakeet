using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Stellar;
using Stellar.Utilities;
using UnityEngine;

namespace Contract
{
    public interface IScvMapCompatable
    {
        SCVal.ScvMap ToScvMap();
    }
    
    public static class SCUtility
    {
        public static bool log = false;
        
        public static void DebugLog(string msg) { if (log) { Debug.Log(msg); } }
        
        public static SCVal NativeToSCVal(object input)
        {
            if (input == null)
            {
                Debug.LogError("input is null!!!!");
                throw new ArgumentNullException(nameof(input));
            }
            Type type = input.GetType();
            if (type.IsEnum)
            {
                uint raw = Convert.ToUInt32(input);
                return new SCVal.ScvU32() { u32 = new uint32(raw) };
            }
            if (input is byte[] byteArray)
            {
                return new SCVal.ScvBytes() { bytes = byteArray, };
            }
            if (input is SCVal.ScvAddress address)
            {
                return address;
            }
            if (type == typeof(uint))
            {
                return new SCVal.ScvU32() { u32 = new uint32((uint)input) };
            }
            if (type == typeof(ulong))
            {
                return new SCVal.ScvU64() { u64 = new uint64((ulong)input) };
            }
            // For native int always convert to SCVal.ScvI32.
            if (type == typeof(int))
            {
                return new SCVal.ScvI32 { i32 = new int32((int)input) };
            }
            if (type == typeof(string))
            {
                return new SCVal.ScvString { str = new SCString((string)input) };
            }
            if (type == typeof(bool))
            {
                return new SCVal.ScvBool { b = (bool)input };
            }
            if (input is AccountAddress accountAddress)
            {
                return accountAddress.ToScvAddress();
            }
            if (input is Array inputArray)
            {
                SCVal[] scValArray = new SCVal[inputArray.Length];
                for (int i = 0; i < inputArray.Length; i++)
                {
                    scValArray[i] = NativeToSCVal(inputArray.GetValue(i));
                }
                return new SCVal.ScvVec() { vec = new SCVec(scValArray) };
            }
            if (input is IScvMapCompatable inputStruct)
            {
                return inputStruct.ToScvMap();
                // List<SCMapEntry> entries = new();
                // foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                // {
                //     object fieldValue = field.GetValue(input);
                //     SCVal scFieldVal = NativeToSCVal(fieldValue);
                //     entries.Add(new SCMapEntry
                //     {
                //         key = new SCVal.ScvSymbol { sym = new SCSymbol(field.Name) },
                //         val = scFieldVal,
                //     });
                // }
                // entries.Sort(new SCMapEntryComparer());
                // return new SCVal.ScvMap { map = new SCMap(entries.ToArray()) };
            }
            else
            {
                throw new NotImplementedException($"Type {input.GetType()} not implemented.");
            }
        }

        public static object SCValToNative(SCVal scVal, Type targetType)
        {
            if (scVal == null)
            {
                Debug.LogError("input is null!!!!");
                throw new ArgumentNullException();
            }
            DebugLog($"SCValToNative: Converting SCVal of discriminator {scVal.Discriminator} to native type {targetType}.");
            if (targetType.IsEnum)
            {
                if (scVal is SCVal.ScvU32 scvU32)
                {
                    DebugLog($"SCValToNative: Attempting to convert {scvU32.u32.InnerValue} to {targetType}.");
                    return Enum.ToObject(targetType, scvU32.u32.InnerValue);
                }
            }
            else if (scVal is SCVal.ScvBytes scvBytes)
            {
                DebugLog($"SCValToNative: Getting bytes with length '{scvBytes.bytes.InnerValue.Length}'.");
                return scvBytes.bytes.InnerValue;
            }
            else if (targetType == typeof(uint))
            {
                DebugLog("SCValToNative: Target type is uint.");
                if (scVal is SCVal.ScvU32 u32Val)
                {
                    DebugLog($"SCValToNative: Found SCVal.ScvU32 with value {u32Val.u32.InnerValue}.");
                    return u32Val.u32.InnerValue;
                }
            }
            else if (targetType == typeof(int))
            {
                DebugLog("SCValToNative: Target type is int.");
                // Prefer I32. If we get a U32, log a warning.
                if (scVal is SCVal.ScvI32 i32Val)
                {
                    DebugLog($"SCValToNative: Found SCVal.ScvI32 with value {i32Val.i32.InnerValue}.");
                    return i32Val.i32.InnerValue;
                }
                else if (scVal is SCVal.ScvU32 u32Val)
                {
                    Debug.LogWarning("SCValToNative: Expected SCVal.ScvI32 for int conversion, got SCVal.ScvU32. Converting anyway.");
                    return u32Val.u32.InnerValue;
                }
                else
                {
                    Debug.LogError("SCValToNative: Failed int conversion. SCVal is not I32 or U32.");
                    throw new NotSupportedException("Expected SCVal.ScvI32 (or SCVal.ScvU32 as fallback) for int conversion.");
                }
            }
            else if (targetType == typeof(string))
            {
                DebugLog("SCValToNative: Target type is string.");
                if (scVal is SCVal.ScvString strVal)
                {
                    DebugLog($"SCValToNative: Found SCVal.ScvString with value '{strVal.str.InnerValue}'.");
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
                DebugLog("SCValToNative: Target type is bool.");
                if (scVal is SCVal.ScvBool boolVal)
                {
                    DebugLog($"SCValToNative: Found SCVal.ScvBool with value {boolVal.b}.");
                    return boolVal.b;
                }
                else
                {
                    Debug.LogError("SCValToNative: Failed bool conversion. SCVal is not SCvBool.");
                    throw new NotSupportedException("Expected SCVal.ScvBool for bool conversion.");
                }
            }
            else if (scVal is SCVal.ScvAddress scvAddress)
            {
                return scvAddress;
            }
            else if (scVal is SCVal.ScvVec scvVec)
            {
                DebugLog("SCValToNative: Target type is a collection. Using vector conversion branch.");
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
                    DebugLog($"SCValToNative: Converting collection element at index {i}.");
                    convertedElements[i] = SCValToNative(innerArray[i], elementType);
                }
                if (targetType.IsArray)
                {
                    Array arr = Array.CreateInstance(elementType, len);
                    for (int i = 0; i < len; i++)
                    {
                        arr.SetValue(convertedElements[i], i);
                    }
                    DebugLog("SCValToNative: Collection converted to array.");
                    return arr;
                }
            }
            // Handle structured types (native structs/classes) via SCVal.ScvMap.
            else if (scVal is SCVal.ScvMap scvMap)
            {
                DebugLog("SCValToNative: Target type is either a map or a structured type.");
                // if is a struct
                if (targetType.IsValueType && !targetType.IsPrimitive)
                {
                    object instance = Activator.CreateInstance(targetType);
                    DebugLog("SCValToNative: Target type is a struct");
                    Dictionary<string, SCMapEntry> dict = new Dictionary<string, SCMapEntry>();
                    foreach (SCMapEntry entry in scvMap.map.InnerValue)
                    {
                        if (entry.key is SCVal.ScvSymbol sym)
                        {
                            dict[sym.sym.InnerValue] = entry;
                            DebugLog($"SCValToNative: Found map key '{sym.sym.InnerValue}'.");
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
                            DebugLog($"SCValToNative: Converting field '{field.Name}'.");
                            object fieldValue = SCValToNative(mapEntry.val, field.FieldType);
                            field.SetValue(instance, fieldValue);
                        }
                        else
                        {
                            if (field.Name != "liveUntilLedgerSeq")
                            {
                                Debug.LogWarning($"SCValToNative: Field '{field.Name}' not found in SCVal map.");
                            }
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
        
        public static SCMapEntry FieldToSCMapEntry(string fieldName, object input)
        {
            return new SCMapEntry()
            {
                key = new SCVal.ScvSymbol() { sym = new SCSymbol(fieldName) },
                val = NativeToSCVal(input),
            };
        }
        
        public static T FromXdrString<T>(string xdrString)
        {
            using MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(xdrString));
            SCVal val = SCValXdr.Decode(new XdrReader(memoryStream));
            return SCValToNative<T>(val);
        }
        
        public static byte[] GetHash(SCVal scVal)
        {
            string xdrString = SCValXdr.EncodeToBase64(scVal);
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(Convert.FromBase64String(xdrString));
        }

        public static byte[] GetHash(IScvMapCompatable obj)
        {
            SCVal scVal = obj.ToScvMap();
            return GetHash(scVal);
        }
        
        public static bool HashEqual(SCVal a, SCVal b)
        {
            string encodedA = SCValXdr.EncodeToBase64(a);
            string encodedB = SCValXdr.EncodeToBase64(b);
            return encodedA == encodedB;
        }
    }
    
    // ReSharper disable InconsistentNaming
    [Serializable]
    public struct MaxRank : IScvMapCompatable
    {
        public uint max;
        public Rank rank;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("max", max),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                }),
            };
        }
    }
    [Serializable]
    public struct Mail : IScvMapCompatable
    {
        public uint mail_type;
        public string message;
        public string sender;
        public uint sent_ledger;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("mail_type", mail_type),
                    SCUtility.FieldToSCMapEntry("message", message),
                    SCUtility.FieldToSCMapEntry("sender", sender),
                    SCUtility.FieldToSCMapEntry("sent_ledger", sent_ledger),
                }),
            };
        }
    }
    [Serializable]
    public struct User
    {
        public uint current_lobby;
        public uint games_completed;
        public long liveUntilLedgerSeq;
        
        public User(byte[] bytes, long nLiveUntilLedgerSeq)
        {
            if (bytes is null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != 8)
                throw new ArgumentException("Packed user must be exactly 8 bytes", nameof(bytes));
            ReadOnlySpan<byte> span = bytes;
            current_lobby = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(0, 4));
            games_completed = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(4, 4));
            liveUntilLedgerSeq = nLiveUntilLedgerSeq;
        }
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
    [Serializable]
    public struct LobbyInfo
    {
        public uint index;
        public SCVal.ScvAddress guest_address;
        public SCVal.ScvAddress host_address;
        public LobbyStatus status;
        public long liveUntilLedgerSeq; // not serialized
        
        public override string ToString()
        {
            var simplified = new
            {
                index,
                guest_address = Globals.AddressToString(guest_address),
                host_address = Globals.AddressToString(host_address),
                status = status.ToString(),
            };
            return JsonConvert.SerializeObject(simplified, Formatting.Indented);
        }
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("index", index),
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("status", status),
                }),
            };
        }
    }
    [Serializable]
    public struct GameState : IScvMapCompatable
    {
        public Phase phase;
        public UserState[] user_states;
        public long liveUntilLedgerSeq; // not serialized
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("phase", phase),
                    SCUtility.FieldToSCMapEntry("user_states", user_states),
                }),
            };
        }
        
        public UserState GetUserState(bool isClientHost)
        {
            int index = isClientHost ? 0 : 1;
            return user_states[index];
        }

        public UserState GetOpponentUserState(bool isClientHost)
        {
            int index = isClientHost ? 1 : 0;
            return user_states[index];
        }
    }
    
    [Serializable]
    public struct Pos: IScvMapCompatable, IEquatable<Pos>
    {
        public int x;
        public int y;

        public Pos(Vector2Int vector)
        {
            x = vector.x;
            y = vector.y;
        }

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("x", x),
                    SCUtility.FieldToSCMapEntry("y", y),
                }),
            };
        }

        public readonly Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public bool Equals(Pos other)
        {
            return x == other.x && y == other.y;
        }
        
        public bool Equals(Vector2Int other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is Pos other && Equals(other) || 
                   obj is Vector2Int vector && Equals(vector);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public static bool operator ==(Pos left, Pos right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Pos left, Pos right)
        {
            return !left.Equals(right);
        }
        
        
    }
    
    [Serializable]
    public struct UserState: IScvMapCompatable
    {
        public PawnCommit[] setup;
        public byte[] setup_hash;
        public uint setup_hash_salt;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("setup", setup),
                    SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
                    SCUtility.FieldToSCMapEntry("setup_hash_salt", setup_hash_salt),
                }),
            };
        }
    }
    
    [Serializable]
    public struct MaxPawns : IScvMapCompatable
    {
        public int max;
        public int rank;

        public MaxPawns(SMaxPawnsPerRank maxPawns)
        {
            max = maxPawns.max;
            rank = (int)maxPawns.rank;
        }
        
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("max", max),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                }),
            };
            return scvMap;
        }
    }
    
    [Serializable]
    public struct Mailbox : IScvMapCompatable
    {
        public string lobby;
        public Mail[] mail;
        
        public SCVal.ScvMap ToScvMap()
        {
            SCVal.ScvMap scvMap = new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby", lobby),
                    SCUtility.FieldToSCMapEntry("mail", mail),
                }),
            };
            return scvMap;
        }
    }
    
    [Serializable]
    public struct ResolveEvent : IScvMapCompatable
    {
        public string defender_pawn_id;
        public uint event_type;
        public Pos original_pos;
        public string pawn_id;
        public Pos target_pos;
        public uint team;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("defender_pawn_id", defender_pawn_id),
                    SCUtility.FieldToSCMapEntry("event_type", event_type),
                    SCUtility.FieldToSCMapEntry("original_pos", original_pos),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("target_pos", target_pos),
                    SCUtility.FieldToSCMapEntry("team", team),
                }),
            };
        }
    }
    [Serializable]
    public struct Tile: IScvMapCompatable
    {
        public int auto_setup_zone;
        public bool is_passable;
        public Pos pos;
        public int setup_team;  // Team enum

        public Tile(global::Tile tile)
        {
            auto_setup_zone = tile.autoSetupZone;
            is_passable = tile.isPassable;
            pos = new Pos(tile.pos);
            setup_team = (int)tile.setupTeam;
        }

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("auto_setup_zone", auto_setup_zone),
                    SCUtility.FieldToSCMapEntry("is_passable", is_passable),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("setup_team", setup_team),
                }),
            };
        }
    }

    [Serializable]
    public struct HiddenRank : IScvMapCompatable
    {
        public Rank rank;
        public uint salt;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("rank", rank),
                    SCUtility.FieldToSCMapEntry("salt", salt),
                }),
            };
        }
    }
    
    [Serializable]
    public struct PawnCommit : IScvMapCompatable
    {
        public byte[] hidden_rank_hash;
        public uint pawn_id;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("hidden_rank_hash", hidden_rank_hash),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                }),
            };
        }
    }
    
    [Serializable]
    public struct Pawn : IScvMapCompatable
    {
        public bool is_alive;
        public bool is_moved;
        public bool is_revealed;
        public string pawn_def_hash;
        public uint pawn_id;
        public Pos pos;
        public uint team; // Team enum

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("is_alive", is_alive),
                    SCUtility.FieldToSCMapEntry("is_moved", is_moved),
                    SCUtility.FieldToSCMapEntry("is_revealed", is_revealed),
                    SCUtility.FieldToSCMapEntry("pawn_def_hash", pawn_def_hash),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("team", team),
                }),
            };
        }
    }
    [Serializable]
    public struct LobbyParameters : IScvMapCompatable
    {
        public byte[] board_hash;
        public bool dev_mode;
        public uint host_team;
        public MaxRank[] max_ranks;
        public bool must_fill_all_tiles;
        public bool security_mode;
        public long liveUntilLedgerSeq; // not serialized
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("board_hash", board_hash),
                    SCUtility.FieldToSCMapEntry("dev_mode", dev_mode),
                    SCUtility.FieldToSCMapEntry("host_team", host_team),
                    SCUtility.FieldToSCMapEntry("max_ranks", max_ranks),
                    SCUtility.FieldToSCMapEntry("must_fill_all_tiles", must_fill_all_tiles),
                    SCUtility.FieldToSCMapEntry("security_mode", security_mode),
                }),
            };
        }
        
        public override string ToString()
        {
            var simplified = new
            {
                board_hash = BitConverter.ToString(board_hash).Replace("-", "").ToLowerInvariant(),
                dev_mode = dev_mode,
                host_team = host_team,
                max_ranks = max_ranks, // assumes MaxRank[] has a clean ToString() or JSON-friendly format
                must_fill_all_tiles = must_fill_all_tiles,
                security_mode = security_mode
            };
            return JsonConvert.SerializeObject(simplified, Formatting.Indented);
        }
    }
    [Serializable]
    public struct TurnMove : IScvMapCompatable
    {
        public bool initialized;
        public string pawn_id;
        public Pos pos;
        public int turn;
        public string user_address;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("initialized", initialized),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                    SCUtility.FieldToSCMapEntry("user_address", user_address),
                }),
            };
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this).ToString();
        }
    }
    [Serializable]
    public struct Turn : IScvMapCompatable
    {
        public ResolveEvent[] guest_events;
        public string guest_events_hash;
        public TurnMove guest_turn;
        public ResolveEvent[] host_events;
        public string host_events_hash;
        public TurnMove host_turn;
        public int turn;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("guest_events", guest_events),
                    SCUtility.FieldToSCMapEntry("guest_events_hash", guest_events_hash),
                    SCUtility.FieldToSCMapEntry("guest_turn", guest_turn),
                    SCUtility.FieldToSCMapEntry("host_events", host_events),
                    SCUtility.FieldToSCMapEntry("host_events_hash", host_events_hash),
                    SCUtility.FieldToSCMapEntry("host_turn", host_turn),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                }),
            };
        }
    }
    [Serializable]
    public struct Lobby : IScvMapCompatable
    {
        public uint game_end_state;
        public string guest_address;
        public UserState guest_state;
        public string host_address;
        public UserState host_state;
        public string index;
        public LobbyParameters parameters;
        public Pawn[] pawns;
        public uint phase; // Phase enum
        public Turn[] turns;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("game_end_state", game_end_state),
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("guest_state", guest_state),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("host_state", host_state),
                    SCUtility.FieldToSCMapEntry("index", index),
                    SCUtility.FieldToSCMapEntry("parameters", parameters),
                    SCUtility.FieldToSCMapEntry("pawns", pawns),
                    SCUtility.FieldToSCMapEntry("phase", phase),
                    SCUtility.FieldToSCMapEntry("turns", turns),
                }),
            };
        }
        public Turn GetLatestTurn()
        {
            return turns.Last();
        }
        
        public Pawn GetPawnById(uint pawn_id)
        {
            return pawns.First(x => x.pawn_id == pawn_id);
        }

        public Pawn? GetPawnByPosition(Vector2Int pos)
        {
            foreach (Pawn p in pawns)
            {
                if (p.pos == new Pos(pos))
                {
                    return p;
                }
            }
            return null;
        }
    }
    
    [Serializable]
    public struct ProveSetupReq : IScvMapCompatable
    {
        
        public uint lobby_id;
        public uint salt;
        public PawnCommit[] setup;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("salt", salt),
                    SCUtility.FieldToSCMapEntry("setup", setup),
                }),
            };
        }
    }
    
    [Serializable]
    public struct MakeLobbyReq : IScvMapCompatable
    {
        public uint lobby_id;
        public LobbyParameters parameters;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("parameters", parameters),
                }),
            };
        }
    }
    
    [Serializable]
    public struct JoinLobbyReq : IScvMapCompatable
    {
        public uint lobby_id;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                }),
            };
        }
    }
    
    [Serializable]
    public struct SetupCommitReq : IScvMapCompatable
    {
        public uint lobby_id;
        public byte[] setup_hash;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
                }),
            };
        }
    }
    
    public struct MoveCommitReq : IScvMapCompatable
    {
        public string lobby;
        public string move_pos_hash;
        public string pawn_id_hash;
        public int turn;
        public string user_address;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby", lobby),
                    SCUtility.FieldToSCMapEntry("move_pos_hash", move_pos_hash),
                    SCUtility.FieldToSCMapEntry("pawn_id_hash", pawn_id_hash),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                    SCUtility.FieldToSCMapEntry("user_address", user_address),
                }),
            };
        }
    }
    
    public struct MoveSubmitReq : IScvMapCompatable
    {
        public string lobby;
        public Pos move_pos;
        public string pawn_id;
        public int turn;
        public string user_address;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby", lobby),
                    SCUtility.FieldToSCMapEntry("move_pos", move_pos),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                    SCUtility.FieldToSCMapEntry("user_address", user_address),
                }),
            };
        }
    }

    public struct MoveResolveReq : IScvMapCompatable
    {
        public ResolveEvent[] events;
        public string events_hash;
        public string lobby;
        public int turn;
        public string user_address;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("events", events),
                    SCUtility.FieldToSCMapEntry("events_hash", events_hash),
                    SCUtility.FieldToSCMapEntry("lobby", lobby),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                    SCUtility.FieldToSCMapEntry("user_address", user_address),
                }),
            };
        }
    }

    public struct SendMailReq : IScvMapCompatable
    {
        public string lobby;
        public Mail mail;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby", lobby),
                    SCUtility.FieldToSCMapEntry("mail", mail),
                }),
            };
        }
    }
    
    public enum ErrorCode {
        UserNotFound = 1,
        InvalidUsername = 2,
        AlreadyInitialized = 3,
        InvalidAddress = 4,
        InvalidExpirationLedger = 5,
        InvalidArgs = 6,
        InviteNotFound = 7,
        LobbyNotFound = 8,
        WrongPhase = 9,
        HostAlreadyInLobby = 10,
        GuestAlreadyInLobby = 11,
        LobbyNotJoinable = 12,
        TurnAlreadyInitialized = 13,
        TurnHashConflict = 14,
    }

    public enum Phase : uint
    {
        Setup = 0,
        Movement = 1,
        Completed = 2,
    }

    public enum LobbyStatus : uint
    {
        WaitingForPlayers = 0,
        GameInProgress = 1,
        HostWin = 2,
        GuestWin = 3,
        Draw = 4,
        Aborted = 5,
    }
}
