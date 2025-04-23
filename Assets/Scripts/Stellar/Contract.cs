using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stellar;
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
            if (type == typeof(uint))
            {
                return new SCVal.ScvU32() { u32 = new uint32((uint)input) };
            }
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
            else if (input is Array inputArray)
            {
                SCVal[] scValArray = new SCVal[inputArray.Length];
                for (int i = 0; i < inputArray.Length; i++)
                {
                    scValArray[i] = NativeToSCVal(inputArray.GetValue(i));
                }
                return new SCVal.ScvVec() { vec = new SCVec(scValArray) };
            }
            else if (input is IScvMapCompatable inputStruct)
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
            DebugLog($"SCValToNative: Converting SCVal of discriminator {scVal.Discriminator} to native type {targetType}.");
            if (targetType == typeof(uint))
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
        
        public static SCMapEntry FieldToSCMapEntry(string fieldName, object input)
        {
            return new SCMapEntry()
            {
                key = new SCVal.ScvSymbol() { sym = new SCSymbol(fieldName) },
                val = NativeToSCVal(input),
            };
        }
        
        public static bool HashEqual(SCVal a, SCVal b)
        {
            string encodedA = SCValXdr.EncodeToBase64(a);
            string encodedB = SCValXdr.EncodeToBase64(b);
            return encodedA == encodedB;
        }
    }
    
    // ReSharper disable InconsistentNaming
    
    
    public struct User: IScvMapCompatable
    {
        public string current_lobby;
        public int games_completed;
        public string index;
        public string name;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("current_lobby", current_lobby),
                    SCUtility.FieldToSCMapEntry("games_completed", games_completed),
                    SCUtility.FieldToSCMapEntry("index", index),
                    SCUtility.FieldToSCMapEntry("name", name),
                }),
            };
        }
    }
    
    [System.Serializable]
    public struct Pos: IScvMapCompatable, IEquatable<Pos>
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
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
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
    
    [System.Serializable]
    public struct UserState: IScvMapCompatable
    {
        public bool committed;
        public uint lobby_state;
        public PawnCommitment[] setup_commitments;
        public uint team;
        public string user_address;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("committed", committed),
                    SCUtility.FieldToSCMapEntry("lobby_state", lobby_state),
                    SCUtility.FieldToSCMapEntry("setup_commitments", setup_commitments),
                    SCUtility.FieldToSCMapEntry("team", team),
                    SCUtility.FieldToSCMapEntry("user_address", user_address),
                }),
            };
        }

        public PawnCommitment GetPawnCommitmentById(string id)
        {
            return setup_commitments.FirstOrDefault(c => c.pawn_id == id);
        }

        public PawnCommitment GetPawnCommitmentById(Guid guid)
        {
            return GetPawnCommitmentById(guid.ToString());
        }
    }
    
    public struct PawnDef: IScvMapCompatable
    {
        public int id;
        public int movement_range;
        public string name;
        public int power;
        public int rank;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("id", id),
                    SCUtility.FieldToSCMapEntry("movement_range", movement_range),
                    SCUtility.FieldToSCMapEntry("name", name),
                    SCUtility.FieldToSCMapEntry("power", power),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                }),
            };
        }
    }
    
    [System.Serializable]
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
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("max", max),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                }),
            };
            return scvMap;
        }
    }
    
    [System.Serializable]
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
                map = new SCMap(new SCMapEntry[]
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
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("auto_setup_zone", auto_setup_zone),
                    SCUtility.FieldToSCMapEntry("is_passable", is_passable),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("setup_team", setup_team),
                }),
            };
        }
    }
    
    [System.Serializable]
    public struct PawnCommitment : IScvMapCompatable
    {
        public string pawn_def_hash;
        public string pawn_id;
        public Pos starting_pos;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("pawn_def_hash", pawn_def_hash),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("starting_pos", starting_pos),
                }),
            };
        }
    }
    
    [System.Serializable]
    public struct Pawn : IScvMapCompatable
    {
        public bool is_alive;
        public bool is_moved;
        public bool is_revealed;
        public string pawn_def_hash;
        public string pawn_id;
        public Pos pos;
        public uint team; // Team enum

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
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
    
    public struct BoardDef : IScvMapCompatable
    {
        public MaxPawns[] default_max_pawns;
        public bool is_hex;
        public string name;
        public Pos size;
        public Tile[] tiles;

        public BoardDef(global::BoardDef def)
        {
            default_max_pawns = new MaxPawns[def.maxPawns.Length];
            for (int i = 0; i < def.maxPawns.Length; i++)
            {
                default_max_pawns[i] = new MaxPawns(def.maxPawns[i]);
            }
            is_hex = def.isHex;
            name = def.name;
            size = new Pos(def.boardSize);
            tiles = new Contract.Tile[def.tiles.Length];
            for (int i = 0; i < def.tiles.Length; i++)
            {
                tiles[i] = new Contract.Tile(def.tiles[i]);
            }
        }
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("default_max_pawns", default_max_pawns),
                    SCUtility.FieldToSCMapEntry("is_hex", is_hex),
                    SCUtility.FieldToSCMapEntry("name", name),
                    SCUtility.FieldToSCMapEntry("size", size),
                    SCUtility.FieldToSCMapEntry("tiles", tiles),
                }),
            };
        }
    }
    [System.Serializable]
    public struct LobbyParameters : IScvMapCompatable
    {
        public string board_def_name;
        public bool dev_mode;
        public MaxPawns[] max_pawns;
        public bool must_fill_all_tiles;
        public bool security_mode;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("board_def_name", board_def_name),
                    SCUtility.FieldToSCMapEntry("dev_mode", dev_mode),
                    SCUtility.FieldToSCMapEntry("max_pawns", max_pawns),
                    SCUtility.FieldToSCMapEntry("must_fill_all_tiles", must_fill_all_tiles),
                    SCUtility.FieldToSCMapEntry("security_mode", security_mode),
                }),
            };
        }
    }
    
    [System.Serializable]
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
                map = new SCMap(new SCMapEntry[]
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
    
    [System.Serializable]
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
                map = new SCMap(new SCMapEntry[]
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
    
    public struct Invite : IScvMapCompatable
    {
        public int expiration_ledger;
        public string guest_address;
        public string host_address;
        public int ledgers_until_expiration;
        public LobbyParameters parameters;
        public int sent_ledger;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("expiration_ledger", expiration_ledger),
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("ledgers_until_expiration", ledgers_until_expiration),
                    SCUtility.FieldToSCMapEntry("parameters", parameters),
                    SCUtility.FieldToSCMapEntry("sent_ledger", sent_ledger),
                }),
            };
        }
    }
    
    [System.Serializable]
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
                map = new SCMap(new SCMapEntry[]
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

        public TurnMove GetLatestTurnMove(Team team)
        {
            string user_address;
            if ((uint)team == guest_state.team)
            {
                user_address = guest_state.user_address;
            }
            else if ((uint)team == host_state.team)
            {
                user_address = host_state.user_address;
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
            Turn turn = turns.Last();
            if (turn.host_turn.user_address == user_address)
            {
                return turn.host_turn;
            }
            else if (turn.guest_turn.user_address == user_address)
            {
                return turn.guest_turn;
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }
        
        public bool IsLobbyStartable()
        {
            
            if (string.IsNullOrEmpty(host_address))
            {
                return false;
            }
            if (string.IsNullOrEmpty(guest_address))
            {
                return false;
            }
            if (game_end_state != 3)
            {
                return false;
            }
            if (host_state.lobby_state == 3)
            {
                return false;
            }
            if (guest_state.lobby_state == 3)
            {
                return false;
            }
            if (phase == 0)
            {
                return false;
            }

            return true;
        }

        public Pawn GetPawnById(string pawn_id)
        {
            return pawns.First(x => x.pawn_id == pawn_id);
        }

        public Pawn GetPawnById(Guid pawn_guid)
        {
            string pawn_id = pawn_guid.ToString();
            return GetPawnById(pawn_id);
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
        
        public UserState GetUserStateByTeam(Team team)
        {
            if (host_state.team == (uint)team)
            {
                return host_state;
            }
            else
            {
                return guest_state;
            }
        }

        public Team GetTeam(string address)
        {
            if (host_state.user_address == address)
            {
                return (Team)host_state.team;
            }
            else if (guest_state.user_address == address)
            {
                return (Team)guest_state.team;
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }
    }

    public struct MakeLobbyReq : IScvMapCompatable
    {
        public string host_address;
        public LobbyParameters parameters;
        public uint salt;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("parameters", parameters),
                    SCUtility.FieldToSCMapEntry("salt", salt),
                }),
            };
        }
    }

    public struct JoinLobbyReq : IScvMapCompatable
    {
        public string guest_address;
        public string lobby_id;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                }),
            };
        }
    }
    
    public struct SendInviteReq : IScvMapCompatable
    {
        public string guest_address;
        public string host_address;
        public int ledgers_until_expiration;
        public LobbyParameters parameters;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("ledgers_until_expiration", ledgers_until_expiration),
                    SCUtility.FieldToSCMapEntry("parameters", parameters),
                }),
            };
        }
    }
    
    public struct AcceptInviteReq : IScvMapCompatable
    {
        public string host_address;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                }),
            };
        }
    }
    
    public struct LeaveLobbyReq : IScvMapCompatable
    {
        public string guest_address;
        public string host_address;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                }),
            };
        }
    }
    
    public struct SetupCommitReq : IScvMapCompatable
    {
        public string lobby_id;
        public PawnCommitment[] setup_commitments;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new SCMapEntry[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("setup_commitments", setup_commitments),
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
                map = new SCMap(new SCMapEntry[]
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
                map = new SCMap(new SCMapEntry[]
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
                map = new SCMap(new SCMapEntry[]
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

    public enum Phase
    {
        Uninitialized = 0,
        Setup = 1,
        Movement = 2,
        Commitment = 3,
        Resolve = 4,
        Ending = 5,
        Aborted = 6,
    }
}
