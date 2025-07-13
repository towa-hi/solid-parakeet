using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
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

    public interface IReq
    {
        LobbyId lobby_id { get; }
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
            switch (input)
            {
                case var _ when type.IsEnum:
                    uint raw = Convert.ToUInt32(input);
                    return new SCVal.ScvU32() { u32 = new uint32(raw) };
                case var _ when type == typeof(uint):
                    return new SCVal.ScvU32() { u32 = new uint32((uint)input) };
                case var _ when type == typeof(ulong):
                    return new SCVal.ScvU64() { u64 = new uint64((ulong)input) };
                case var _ when type == typeof(int):
                    return new SCVal.ScvI32 { i32 = new int32((int)input) };
                case var _ when type == typeof(string):
                    return new SCVal.ScvString { str = new SCString((string)input) };
                case var _ when type == typeof(bool):
                    return new SCVal.ScvBool { b = (bool)input };
                case byte[] byteArray:
                    return new SCVal.ScvBytes() { bytes = byteArray };
                case Vector2Int vector2Int:
                    // special case where we directly convert vector2Int into
                    // something that can be interpreted as a Pos struct
                    return new SCVal.ScvMap
                    {
                        map = new SCMap(new[]
                        {
                            FieldToSCMapEntry("x", vector2Int.x),
                            FieldToSCMapEntry("y", vector2Int.y),
                        }),
                    };
                case TileState tile:
                    // special case for unpacked tiles
                    return new SCVal.ScvU32() { u32 = new uint32((uint)tile) };
                case SCVal.ScvAddress address:
                    return address;
                case AccountAddress accountAddress:
                    return accountAddress.ToScvAddress();
                case PawnId pawnId:
                    return new SCVal.ScvU32() { u32 = new uint32(pawnId.Value) };
                case LobbyId lobbyId:
                    return new SCVal.ScvU32() { u32 = new uint32(lobbyId.Value) };
                case PawnState pawnState:
                    return new SCVal.ScvU32() { u32 = new uint32(pawnState) };
                case Array inputArray:
                    SCVal[] scValArray = new SCVal[inputArray.Length];
                    for (int i = 0; i < inputArray.Length; i++)
                    {
                        scValArray[i] = NativeToSCVal(inputArray.GetValue(i));
                    }
                    return new SCVal.ScvVec() { vec = new SCVec(scValArray) };
                case IScvMapCompatable inputStruct:
                    return inputStruct.ToScvMap();
                default:
                    throw new NotImplementedException($"Type {type} not implemented.");
            }
        }

        static object SCValToNative(SCVal scVal, Type targetType)
        {
            if (scVal == null)
            {
                Debug.LogError("input is null!!!!");
                throw new ArgumentNullException();
            }
            DebugLog($"SCValToNative: Converting SCVal of discriminator {scVal.Discriminator} to native type {targetType}.");
            
            switch (targetType)
            {
                case var _ when targetType.IsEnum:
                    if (scVal is SCVal.ScvU32 scvU32)
                    {
                        DebugLog($"SCValToNative: Attempting to convert {scvU32.u32.InnerValue} to {targetType}.");
                        return Enum.ToObject(targetType, scvU32.u32.InnerValue);
                    }
                    break;
                case var _ when targetType == typeof(uint):
                    DebugLog("SCValToNative: Target type is uint.");
                    if (scVal is SCVal.ScvU32 uintVal)
                    {
                        DebugLog($"SCValToNative: Found SCVal.ScvU32 with value {uintVal.u32.InnerValue}.");
                        return uintVal.u32.InnerValue;
                    }
                    break;
                case var _ when targetType == typeof(int):
                    DebugLog("SCValToNative: Target type is int.");
                    // Prefer I32. If we get a U32, log a warning.
                    if (scVal is SCVal.ScvI32 i32Val)
                    {
                        DebugLog($"SCValToNative: Found SCVal.ScvI32 with value {i32Val.i32.InnerValue}.");
                        return i32Val.i32.InnerValue;
                    }
                    else if (scVal is SCVal.ScvU32 intU32Val)
                    {
                        Debug.LogWarning("SCValToNative: Expected SCVal.ScvI32 for int conversion, got SCVal.ScvU32. Converting anyway.");
                        return intU32Val.u32.InnerValue;
                    }
                    break;
                case var _ when targetType == typeof(ulong):
                    DebugLog("SCValToNative: Target type is ulong.");
                    if (scVal is SCVal.ScvU64 u64Val)
                    {
                        DebugLog($"SCValToNative: Found SCVal.ScvU64 with value '{u64Val.u64.InnerValue}'.");
                        return u64Val.u64.InnerValue;
                    }
                    break;
                case var _ when targetType == typeof(string):
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
                    
                case var _ when targetType == typeof(bool):
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
                    
                case var _ when targetType == typeof(AccountAddress):
                    if (scVal is SCVal.ScvAddress scvAddress)
                    {
                        return new AccountAddress(scvAddress);
                    }
                    break;
                    
                case var _ when targetType == typeof(PawnId):
                    if (scVal is SCVal.ScvU32 pawnIdU32Val)
                    {
                        return new PawnId(pawnIdU32Val.u32.InnerValue);
                    }
                    break;
                    
                case var _ when targetType == typeof(LobbyId):
                    if (scVal is SCVal.ScvU32 lobbyU32Val)
                    {
                        return new LobbyId(lobbyU32Val.u32.InnerValue);
                    }
                    break;
                case var _ when targetType == typeof(TileState):
                    if (scVal is SCVal.ScvU32 tileU32Val)
                    {
                        return (TileState)tileU32Val.u32.InnerValue;
                    }
                    break;
                case var _ when targetType == typeof(PawnState):
                    if (scVal is SCVal.ScvU32 pawnU32Val)
                    {
                        return (PawnState)pawnU32Val.u32.InnerValue;
                    }
                    break;
                case var _ when targetType == typeof(byte[]):
                    DebugLog("SCValToNative: Target type is byte[].");
                    if (scVal is SCVal.ScvBytes scvBytes)
                    {
                        DebugLog($"SCValToNative: Found SCVal.ScvBytes with length {scvBytes.bytes.InnerValue.Length}.");
                        return scvBytes.bytes.InnerValue;
                    }
                    break;
                default:
                    switch (scVal)
                    {
                        case SCVal.ScvBytes scvBytes2:
                            DebugLog($"SCValToNative: Getting bytes with length '{scvBytes2.bytes.InnerValue.Length}'.");
                            return scvBytes2.bytes.InnerValue;
                            
                        case SCVal.ScvVec scvVec:
                            DebugLog("SCValToNative: Target type is a collection. Using vector conversion branch.");
                            Type elementType = targetType.IsArray
                                ? targetType.GetElementType()
                                : (targetType.IsGenericType ? targetType.GetGenericArguments()[0] : typeof(object));
                            if (elementType == null)
                            {
                                Debug.LogError("SCValToNative: Unable to determine element type for collection conversion.");
                                throw new NotSupportedException("Unable to determine element type for collection conversion.");
                            }
                            SCVal[] vecInnerArray = scvVec.vec.InnerValue;
                            int len = vecInnerArray.Length;
                            object[] convertedElements = new object[len];
                            for (int i = 0; i < len; i++)
                            {
                                DebugLog($"SCValToNative: Converting collection element at index {i}.");
                                convertedElements[i] = SCValToNative(vecInnerArray[i], elementType);
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
                            break;
                            
                        case SCVal.ScvMap scvMap:
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
                                // special exception for vector2ints
                                if (targetType == typeof(Vector2Int))
                                {
                                    if (dict.TryGetValue("x", out SCMapEntry xEntry) && dict.TryGetValue("y", out SCMapEntry yEntry))
                                    {
                                        int x = (int)SCValToNative(xEntry.val, typeof(int));
                                        int y = (int)SCValToNative(yEntry.val, typeof(int));
                                        return new Vector2Int(x, y);
                                    }
                                    else
                                    {
                                        Debug.LogError("SCValToNative: Vector2Int conversion requires 'x' and 'y' fields in SCVal map.");
                                        throw new NotSupportedException("Vector2Int conversion requires 'x' and 'y' fields in SCVal map.");
                                    }
                                }
                                foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                                {
                                    if (dict.TryGetValue(field.Name, out SCMapEntry mapEntry))
                                    {
                                        DebugLog($"SCValToNative: Converting field '{field.Name}'.");
                                        
                                        // Check if field is nullable and the SCVal is a Vec
                                        bool isNullableField = (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) ||
                                                               field.GetCustomAttribute<CanBeNullAttribute>() != null;
                                        
                                        if (isNullableField && mapEntry.val is SCVal.ScvVec scvVecNullable)
                                        {
                                            // Handle nullable field as single-item Vec
                                            SCVal[] nullableInnerArray = scvVecNullable.vec.InnerValue;
                                            if (nullableInnerArray.Length == 1)
                                            {
                                                DebugLog($"SCValToNative: Unwrapping single-item Vec for nullable field '{field.Name}'.");
                                                // For nullable types, get the underlying type
                                                Type underlyingType = field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>)
                                                    ? Nullable.GetUnderlyingType(field.FieldType)
                                                    : field.FieldType;
                                                object fieldValue = SCValToNative(nullableInnerArray[0], underlyingType);
                                                field.SetValue(instance, fieldValue);
                                            }
                                            else if (nullableInnerArray.Length == 0)
                                            {
                                                // Empty Vec means null for nullable field
                                                DebugLog($"SCValToNative: Empty Vec treated as null for nullable field '{field.Name}'.");
                                                field.SetValue(instance, null);
                                            }
                                            else
                                            {
                                                Debug.LogWarning($"SCValToNative: Vec for nullable field '{field.Name}' has {nullableInnerArray.Length} items, expected 0 or 1.");
                                            }
                                        }
                                        else
                                        {
                                            object fieldValue = SCValToNative(mapEntry.val, field.FieldType);
                                            field.SetValue(instance, fieldValue);
                                        }
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
                            break;
                    }
                    break;
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
            // Try to determine if field is nullable by examining the input type
            bool isNullable = false;
            
            if (input != null)
            {
                Type inputType = input.GetType();
                // Check if it's a Nullable<T> type
                isNullable = inputType.IsGenericType && inputType.GetGenericTypeDefinition() == typeof(Nullable<>);
            }
            else
            {
                // Input is null - assume it should be serialized as Vec for nullable fields
                isNullable = true;
            }
            
            if (isNullable)
            {
                if (input == null)
                {
                    // Serialize null as empty Vec
                    return new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol(fieldName) },
                        val = new SCVal.ScvVec() { vec = new SCVec(Array.Empty<SCVal>()) },
                    };
                }
                else
                {
                    // Serialize non-null nullable as single-item Vec
                    return new SCMapEntry()
                    {
                        key = new SCVal.ScvSymbol() { sym = new SCSymbol(fieldName) },
                        val = new SCVal.ScvVec() { vec = new SCVec(new SCVal[] { NativeToSCVal(input) }) },
                    };
                }
            }
            else
            {
                return new SCMapEntry()
                {
                    key = new SCVal.ScvSymbol() { sym = new SCSymbol(fieldName) },
                    val = NativeToSCVal(input),
                };
            }
        }
        
        public static T FromXdrString<T>(string xdrString)
        {
            using MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(xdrString));
            SCVal val = SCValXdr.Decode(new XdrReader(memoryStream));
            return SCValToNative<T>(val);
        }
        
        public static byte[] Get16ByteHash(IScvMapCompatable obj)
        {
            SCVal scVal = obj.ToScvMap();
            string xdrString = SCValXdr.EncodeToBase64(scVal);
            using SHA256 sha256 = SHA256.Create();
            byte[] fullHash = sha256.ComputeHash(Convert.FromBase64String(xdrString));
            // Truncate to 16 bytes for HiddenRankHash
            byte[] truncatedHash = new byte[16];
            Array.Copy(fullHash, truncatedHash, 16);
            return truncatedHash;
        }

        public static bool HashEqual(SCVal a, SCVal b)
        {
            string encodedA = SCValXdr.EncodeToBase64(a);
            string encodedB = SCValXdr.EncodeToBase64(b);
            return encodedA == encodedB;
        }
    }
    
    // ReSharper disable InconsistentNaming
    
    public enum Phase : uint
    {
        Lobby = 0,
        SetupCommit = 1,
        MoveCommit = 2,
        MoveProve = 3,
        RankProve = 4,
        Finished = 5,
        Aborted = 6,
    }

    public enum Subphase : uint
    {
        Host = 0,
        Guest = 1,
        Both = 2,
        None = 3,
    }
    
    [Serializable]
    public readonly struct PawnId : IEquatable<PawnId>
    {
        public readonly uint Value;
        
        public PawnId(uint value) => Value = value;

        public (Vector2Int, Team) Decode()
        {
            // Must match Rust encoding: bit 0=team, bits 1-4=x, bits 5-8=y
            bool isHost = (Value & 1) == 0;
            int x = (int)((Value >> 1) & 0xF); // Extract bits 1-4 (4 bits)
            int y = (int)((Value >> 5) & 0xF); // Extract bits 5-8 (4 bits)
            Vector2Int startPos = new Vector2Int(x, y);
            Team t = isHost ? Team.RED : Team.BLUE;
            return (startPos, t);
        }

        public Vector2Int GetStartPos()
        {
            return Decode().Item1;
        }

        public Team GetTeam()
        {
            return Decode().Item2;
        }
        
        public static implicit operator uint(PawnId pawnId) => pawnId.Value;
        public static implicit operator PawnId(uint value) => new(value);
        
        public bool Equals(PawnId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PawnId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        
        public static bool operator ==(PawnId left, PawnId right) => left.Equals(right);
        public static bool operator !=(PawnId left, PawnId right) => !left.Equals(right);
    }
    
    [Serializable]
    public readonly struct LobbyId : IEquatable<LobbyId>
    {
        private readonly uint val;
        public uint Value => val;

        public LobbyId(uint value)
        {
            val = value;
        }

        // Allow easy conversion to/from raw uint:
        public static implicit operator uint(LobbyId id)   => id.val;
        public static explicit operator LobbyId(uint raw)  => new LobbyId(raw);

        // IEquatable<T> implementation:
        public bool Equals(LobbyId other) 
            => val == other.val;

        public override bool Equals(object obj) 
            => obj is LobbyId other && Equals(other);

        public override int GetHashCode() 
            => val.GetHashCode();

        // == and != operators:
        public static bool operator ==(LobbyId left, LobbyId right) 
            => left.Equals(right);
        public static bool operator !=(LobbyId left, LobbyId right) 
            => !left.Equals(right);

        public override string ToString() 
            => val.ToString();
    }

    [Serializable]
    public struct TileState
    {
        public bool passable;
        public Vector2Int pos;
        public Team setup;
        public uint setup_zone;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("passable", passable),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("setup", setup),
                    SCUtility.FieldToSCMapEntry("setup_zone", setup_zone),
                }),
            };
        }

        // Implicit conversion from Tile to uint (packing)
        public static implicit operator uint(TileState tile)
        {
            uint packed = 0;
            
            // Pack passable (1 bit) - bit 0
            if (tile.passable)
            {
                packed |= 1u;
            }
            
            // Pack x coordinate (9 bits) - bits 1-9
            uint xVal = ((uint)tile.pos.x) & 0x1FFu;
            packed |= xVal << 1;
            
            // Pack y coordinate (9 bits) - bits 10-18
            uint yVal = ((uint)tile.pos.y) & 0x1FFu;
            packed |= yVal << 10;
            
            // Pack setup (3 bits) - bits 19-21
            uint setupVal = (uint)tile.setup & 0x7u;
            packed |= setupVal << 19;
            
            // Pack setup_zone (3 bits) - bits 22-24
            uint setupZoneVal = tile.setup_zone & 0x7u;
            packed |= setupZoneVal << 22;
            
            return packed;
        }

        // Implicit conversion from uint to Tile (unpacking)
        public static implicit operator TileState(uint packed)
        {
            // Extract passable (bit 0)
            bool passable = (packed & 1u) != 0;
            
            // Extract x coordinate (bits 1-9)
            int x = (int)((packed >> 1) & 0x1FFu);
            
            // Extract y coordinate (bits 10-18)
            int y = (int)((packed >> 10) & 0x1FFu);
            
            // Extract setup (bits 19-21)
            uint setup = (packed >> 19) & 0x7u;
            
            // Extract setup_zone (bits 22-24)
            uint setupZone = (packed >> 22) & 0x7u;
            
            return new TileState
            {
                passable = passable,
                pos = new Vector2Int(x, y),
                setup = (Team)setup,
                setup_zone = setupZone,
            };
        }
    }
    
    [Serializable]
    public struct MerkleProof : IScvMapCompatable
    {
        public uint leaf_index;
        public byte[][] siblings;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap()
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("leaf_index", leaf_index),
                    SCUtility.FieldToSCMapEntry("siblings", siblings),
                }),
            };
        }
    }
    
    [Serializable]
    public struct User : IScvMapCompatable
    {
        public LobbyId current_lobby;
        public uint games_completed;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("current_lobby", current_lobby),
                    SCUtility.FieldToSCMapEntry("games_completed", games_completed),
                }),
            };
        }
    }

    [Serializable]
    public struct Board : IScvMapCompatable
    {
        public bool hex;
        public string name;
        public Vector2Int size;
        public TileState[] tiles;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("hex", hex),
                    SCUtility.FieldToSCMapEntry("name", name),
                    SCUtility.FieldToSCMapEntry("size", size),
                    SCUtility.FieldToSCMapEntry("tiles", tiles),
                }),
            };
        }
        
        public TileState? GetTileFromPosition(Vector2Int pos)
        {
            if (tiles.Any(tile => tile.pos == pos))
            {
                return tiles.First(tile => tile.pos == pos);
            }
            return null;
        }
    }

    [Serializable]
    public struct HiddenMove : IScvMapCompatable
    {
        public PawnId pawn_id;
        public ulong salt;
        public Vector2Int start_pos;
        public Vector2Int target_pos;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("salt", salt),
                    SCUtility.FieldToSCMapEntry("start_pos", start_pos),
                    SCUtility.FieldToSCMapEntry("target_pos", target_pos),
                }),
            };
        }
    }

    [Serializable]
    public struct HiddenRank : IScvMapCompatable
    {
        public PawnId pawn_id;
        public Rank rank;
        public ulong salt;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                    SCUtility.FieldToSCMapEntry("salt", salt),
                }),
            };
        }
    }

    [Serializable]
    public struct SetupCommit : IScvMapCompatable
    {
        public byte[] hidden_rank_hash;
        public PawnId pawn_id;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
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
    public struct PawnState : IEquatable<PawnState>
    {
        public bool alive;
        public bool moved;
        public bool moved_scout;
        public PawnId pawn_id;
        public Vector2Int pos;
        public Rank? rank;

        public Team GetTeam()
        {
            return pawn_id.Decode().Item2;
        }

        public Vector2Int GetStartPosition()
        {
            return pawn_id.Decode().Item1;
        }

        public Rank? GetKnownRank()
        {
            if (rank is Rank knownRank)
            {
                return knownRank;
            }

            if (CacheManager.GetHiddenRankAndProof(pawn_id) is CachedRankProof rankProof)
            {
                return rankProof.hidden_rank.rank;
            }
            return null;
        }
        
        public bool CanMove()
        {
            if (alive && GetKnownRank() is Rank knownRank)
            {
                if (knownRank != Rank.TRAP && knownRank != Rank.THRONE)
                {
                    return true;
                }
            }
            return false;
        }
        

        // Implicit conversion to uint (packing) - matches Rust contract bitpacking
        public static implicit operator uint(PawnState pawn)
        {
            uint packed = 0;
            // Pack pawn_id (9 bits at head)
            uint pawnIdPacked = pawn.pawn_id.Value & 0x1FFu;
            packed |= pawnIdPacked << 0;
            // Pack flags (bits 9-11)
            if (pawn.alive) packed |= 1u << 9;
            if (pawn.moved) packed |= 1u << 10;
            if (pawn.moved_scout) packed |= 1u << 11;
            // Pack coordinates (4 bits each, range 0-15) - MUST match Rust contract
            packed |= ((uint)pawn.pos.x & 0xFu) << 12;
            packed |= ((uint)pawn.pos.y & 0xFu) << 16;
            // Pack rank (4 bits at position 20)
            uint rankValue = pawn.rank.HasValue ? (uint)pawn.rank.Value : 12u;
            packed |= (rankValue & 0xFu) << 20;
            return packed;
        }

        // Implicit conversion from uint (unpacking) - matches Rust contract bitpacking
        public static implicit operator PawnState(uint packed)
        {
            // Extract pawn_id (9 bits at head)
            uint pawnId = packed & 0x1FFu;
            // Extract flags
            bool alive = ((packed >> 9) & 1) != 0;
            bool moved = ((packed >> 10) & 1) != 0;
            bool movedScout = ((packed >> 11) & 1) != 0;
            // Extract coordinates (4 bits each, range 0-15) - MUST match Rust contract
            int x = (int)((packed >> 12) & 0xFu);
            int y = (int)((packed >> 16) & 0xFu);
            // Extract rank (4 bits at position 20)
            uint rankVal = (packed >> 20) & 0xFu;
            Rank? rank = rankVal == 12 ? null : (Rank?)rankVal;
            return new PawnState
            {
                alive = alive,
                moved = moved,
                moved_scout = movedScout,
                pawn_id = new PawnId(pawnId),
                pos = new Vector2Int(x, y),
                rank = rank,
            };
        }

        public bool Equals(PawnState other)
        {
            return (uint)this == (uint)other;
        }

        public override bool Equals(object obj)
        {
            return obj is PawnState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((uint)this).GetHashCode();
        }

        public static bool operator ==(PawnState left, PawnState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PawnState left, PawnState right)
        {
            return !left.Equals(right);
        }
    }
    
    [Serializable]
    public struct UserMove : IScvMapCompatable
    {
        public byte[] move_hash; // is all zeros when empty
        public HiddenMove? move_proof;
        public PawnId[] needed_rank_proofs;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("move_hash", move_hash),
                    SCUtility.FieldToSCMapEntry("move_proof", move_proof),
                    SCUtility.FieldToSCMapEntry("needed_rank_proofs", needed_rank_proofs),
                }),
            };
        }
    }

    [Serializable]
    public struct GameState : IScvMapCompatable
    {
        public UserMove[] moves;
        public PawnState[] pawns;
        public byte[][] rank_roots;
        public uint turn;
        public long liveUntilLedgerSeq;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("moves", moves),
                    SCUtility.FieldToSCMapEntry("pawns", pawns),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                }),
            };
        }

        public UserMove GetUserMove(bool isHost)
        {
            return isHost ? moves[0] : moves[1];
        }
    }

    [Serializable]
    public struct LobbyParameters : IScvMapCompatable
    {
        public Board board;
        public byte[] board_hash;
        public bool dev_mode;
        public Team host_team;
        public uint[] max_ranks; // NOTE: index is the Rank enum converted to int
        public bool must_fill_all_tiles;
        public bool security_mode;
        public long liveUntilLedgerSeq;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("board", board),
                    SCUtility.FieldToSCMapEntry("board_hash", board_hash),
                    SCUtility.FieldToSCMapEntry("dev_mode", dev_mode),
                    SCUtility.FieldToSCMapEntry("host_team", host_team),
                    SCUtility.FieldToSCMapEntry("max_ranks", max_ranks),
                    SCUtility.FieldToSCMapEntry("must_fill_all_tiles", must_fill_all_tiles),
                    SCUtility.FieldToSCMapEntry("security_mode", security_mode),
                }),
            };
        }
        
        public int GetMax(Rank rank)
        {
            return (int)max_ranks[(int)rank];
        }
    }

    [Serializable]
    public struct LobbyInfo : IScvMapCompatable
    {
        public AccountAddress? guest_address;
        public AccountAddress? host_address;
        public LobbyId index;
        public Phase phase;
        public Subphase subphase;
        public long liveUntilLedgerSeq;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("guest_address", guest_address),
                    SCUtility.FieldToSCMapEntry("host_address", host_address),
                    SCUtility.FieldToSCMapEntry("index", index),
                    SCUtility.FieldToSCMapEntry("phase", phase),
                    SCUtility.FieldToSCMapEntry("subphase", subphase),
                }),
            };
        }

        public bool IsHost(AccountAddress address)
        {
            if (address == host_address)
            {
                return true;
            }
            else if (address == guest_address)
            {
                return false;
            }
            else throw new ArgumentOutOfRangeException(nameof(address));
        }
    }

    [Serializable]
    public struct MakeLobbyReq : IScvMapCompatable, IReq
    {
        public LobbyId lobby_id { get; set; }
        public LobbyParameters parameters;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
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
    public struct JoinLobbyReq : IScvMapCompatable, IReq
    {
        public LobbyId lobby_id { get; set; }

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                }),
            };
        }
    }

    [Serializable]
    public struct CommitSetupReq : IScvMapCompatable, IReq
    {
        public LobbyId lobby_id { get; set; }
        public byte[] rank_commitment_root;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("rank_commitment_root", rank_commitment_root),
                }),
            };
        }
    }

    [Serializable]
    public struct CommitMoveReq : IScvMapCompatable, IReq
    {
        public LobbyId lobby_id { get; set; }
        public byte[] move_hash;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("move_hash", move_hash),
                }),
            };
        }
    }

    [Serializable]
    public struct ProveMoveReq : IScvMapCompatable, IReq
    {
        public LobbyId lobby_id { get; set; }
        public HiddenMove move_proof;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("move_proof", move_proof),
                }),
            };
        }
    }

    [Serializable]
    public struct ProveRankReq : IScvMapCompatable, IReq
    {
        public HiddenRank[] hidden_ranks;
        public LobbyId lobby_id { get; set; }
        public MerkleProof[] merkle_proofs;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("hidden_ranks", hidden_ranks),
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("merkle_proofs", merkle_proofs),
                }),
            };
        }
    }

    // not a contract struct but we serialize it as xdr to save
    [Serializable]
    public struct CachedRankProof : IScvMapCompatable
    {
        public HiddenRank hidden_rank;
        public MerkleProof merkle_proof;
        
        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("hidden_rank", hidden_rank),
                    SCUtility.FieldToSCMapEntry("merkle_proof", merkle_proof),
                }),
            };
        }
    }

    [Serializable]
    public enum ContractFunction
    {
        make_lobby,
        leave_lobby,
        join_lobby,
        commit_setup,
        commit_move,
        commit_move_and_prove_move,
        prove_move,
        prove_move_and_prove_rank,
        prove_rank,
        simulate_collisions,
    }

    [Serializable]
    public enum ErrorCodes
    {
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
        LobbyAlreadyExists = 15,
        LobbyHasNoHost = 16,
        JoinerIsHost = 17,
        SetupStateNotFound = 18,
        GetPlayerIndexError = 19,
        AlreadyCommittedSetup = 20,
        NotInLobby = 21,
        NoSetupCommitment = 22,
        NoOpponentSetupCommitment = 23,
        SetupHashFail = 24,
        GameStateNotFound = 25,
        GameNotInProgress = 26,
        AlreadySubmittedSetup = 27,
        InvalidContractState = 28,
        WrongInstruction = 29,
        HiddenMoveHashFail = 30,
        PawnNotTeam = 31,
        PawnNotFound = 32,
        RedMoveInvalid = 33,
        BlueMoveInvalid = 34,
        BothMovesInvalid = 35,
        HiddenRankHashFail = 36,
        PawnCommitNotFound = 37,
        WrongPawnId = 38,
        InvalidPawnId = 39,
        InvalidBoard = 40,
        WrongSubphase = 41,
        NoRankProofsNeeded = 42,
        ParametersInvalid = 43,
    }
}
