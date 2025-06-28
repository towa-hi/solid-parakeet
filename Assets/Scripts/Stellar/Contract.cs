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
                case Tile tile:
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
                    else
                    {
                        Debug.LogError("SCValToNative: Failed int conversion. SCVal is not I32 or U32.");
                        throw new NotSupportedException("Expected SCVal.ScvI32 (or SCVal.ScvU32 as fallback) for int conversion.");
                    }
                    
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
                    if (scVal is SCVal.ScvU32 pawnU32Val)
                    {
                        return new PawnId(pawnU32Val.u32.InnerValue);
                    }
                    break;
                    
                case var _ when targetType == typeof(LobbyId):
                    if (scVal is SCVal.ScvU32 lobbyU32Val)
                    {
                        return new LobbyId(lobbyU32Val.u32.InnerValue);
                    }
                    break;
                case var _ when targetType == typeof(Tile):
                    if (scVal is SCVal.ScvU32 tileU32Val)
                    {
                        return (Tile)tileU32Val.u32.InnerValue;
                    }
                    break;
                default:
                    switch (scVal)
                    {
                        case SCVal.ScvBytes scvBytes:
                            DebugLog($"SCValToNative: Getting bytes with length '{scvBytes.bytes.InnerValue.Length}'.");
                            return scvBytes.bytes.InnerValue;
                            
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

        public static byte[] GetHiddenRankHash(HiddenRank hiddenRank)
        {
            SCVal scVal = hiddenRank.ToScvMap();
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
    
    [Serializable]
    public readonly struct PawnId : IEquatable<PawnId>
    {
        public readonly uint Value;
        
        public PawnId(uint value) => Value = value;
        
        public static implicit operator uint(PawnId pawnId) => pawnId.Value;
        public static implicit operator PawnId(uint value) => new(value);
        
        public bool Equals(PawnId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PawnId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        
        public static bool operator ==(PawnId left, PawnId right) => left.Equals(right);
        public static bool operator !=(PawnId left, PawnId right) => !left.Equals(right);
    }
    
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
    
    // [Serializable]
    // public struct Pos : IScvMapCompatable, IEquatable<Pos>
    // {
    //     public int x;
    //     public int y;
    //
    //     public Pos(Vector2Int pos)
    //     {
    //         x = (int)pos.x;
    //         y = (int)pos.y;
    //     }
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("x", x),
    //                 SCUtility.FieldToSCMapEntry("y", y),
    //             }),
    //         };
    //     }
    //
    //     public bool Equals(Pos other) => x == other.x && y == other.y;
    //     public override bool Equals(object obj) => obj is Pos other && Equals(other);
    //     public override int GetHashCode() => HashCode.Combine(x, y);
    //     
    //     public Vector2Int ToVector2Int() => new Vector2Int(x, y);
    // }

    [Serializable]
    public struct Tile
    {
        public bool passable;
        public Vector2Int pos;
        public uint setup;
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
        public static implicit operator uint(Tile tile)
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
            uint setupVal = tile.setup & 0x7u;
            packed |= setupVal << 19;
            
            // Pack setup_zone (3 bits) - bits 22-24
            uint setupZoneVal = tile.setup_zone & 0x7u;
            packed |= setupZoneVal << 22;
            
            return packed;
        }

        // Implicit conversion from uint to Tile (unpacking)
        public static implicit operator Tile(uint packed)
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
            
            return new Tile
            {
                passable = passable,
                pos = new Vector2Int(x, y),
                setup = setup,
                setup_zone = setupZone,
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
        public Tile[] tiles;

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
    public struct Setup : IScvMapCompatable
    {
        public ulong salt;
        public SetupCommit[] setup_commits;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("salt", salt),
                    SCUtility.FieldToSCMapEntry("setup_commits", setup_commits),
                }),
            };
        }
    }

    [Serializable]
    public struct PawnState : IScvMapCompatable
    {
        public bool alive;
        public byte[] hidden_rank_hash;
        public bool moved;
        public bool moved_scout;
        public PawnId pawn_id;
        public Vector2Int pos;
        public Rank? rank;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("alive", alive),
                    SCUtility.FieldToSCMapEntry("hidden_rank_hash", hidden_rank_hash),
                    SCUtility.FieldToSCMapEntry("moved", moved),
                    SCUtility.FieldToSCMapEntry("moved_scout", moved_scout),
                    SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
                    SCUtility.FieldToSCMapEntry("pos", pos),
                    SCUtility.FieldToSCMapEntry("rank", rank),
                }),
            };
        }
    }

    [Serializable]
    public struct UserSetup : IScvMapCompatable
    {
        public Setup[] setup;
        public byte[] setup_hash;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("setup", setup),
                    SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
                }),
            };
        }
    }

    [Serializable]
    public struct UserMove : IScvMapCompatable
    {
        public byte[] move_hash;
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
        public UserSetup[] setups;
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
                    SCUtility.FieldToSCMapEntry("setups", setups),
                    SCUtility.FieldToSCMapEntry("turn", turn),
                }),
            };
        }

        public UserMove GetUserMove(bool isHost)
        {
            return isHost ? moves[0] : moves[1];
        }

        public UserSetup GetUserSetup(bool isHost)
        {
            return isHost ? setups[0] : setups[1];
        }
    }

    [Serializable]
    public struct LobbyParameters : IScvMapCompatable
    {
        public Board board;
        public byte[] board_hash;
        public bool dev_mode;
        public uint host_team;
        public uint[] max_ranks;
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
    }

    [Serializable]
    public struct LobbyInfo : IScvMapCompatable
    {
        public AccountAddress? guest_address;
        public AccountAddress? host_address;
        public uint index;
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
    public struct MakeLobbyReq : IScvMapCompatable
    {
        public LobbyId lobby_id;
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
    public struct JoinLobbyReq : IScvMapCompatable
    {
        public LobbyId lobby_id;

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
    public struct CommitSetupReq : IScvMapCompatable
    {
        public LobbyId lobby_id;
        public byte[] setup_hash;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
                }),
            };
        }
    }

    [Serializable]
    public struct ProveSetupReq : IScvMapCompatable
    {
        public LobbyId lobby_id;
        public Setup setup;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                    SCUtility.FieldToSCMapEntry("setup", setup),
                }),
            };
        }
    }

    [Serializable]
    public struct CommitMoveReq : IScvMapCompatable
    {
        public LobbyId lobby_id;
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
    public struct ProveMoveReq : IScvMapCompatable
    {
        public LobbyId lobby_id;
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
    public struct ProveRankReq : IScvMapCompatable
    {
        public HiddenRank[] hidden_ranks;
        public LobbyId lobby_id;

        public SCVal.ScvMap ToScvMap()
        {
            return new SCVal.ScvMap
            {
                map = new SCMap(new[]
                {
                    SCUtility.FieldToSCMapEntry("hidden_ranks", hidden_ranks),
                    SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
                }),
            };
        }
    }

    public enum Phase : uint
    {
        Lobby = 0,
        SetupCommit = 1,
        SetupProve = 2,
        MoveCommit = 3,
        MoveProve = 4,
        RankProve = 5,
        Finished = 6,
        Aborted = 7,
        None = 8,
    }

    public enum Subphase : uint
    {
        Host = 0,
        Guest = 1,
        Both = 2,
        None = 3,
    }
    // [Serializable]
    // public struct MaxRank : IScvMapCompatable
    // {
    //     public uint max;
    //     public Rank rank;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("max", max),
    //                 SCUtility.FieldToSCMapEntry("rank", rank),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct Mail : IScvMapCompatable
    // {
    //     public uint mail_type;
    //     public string message;
    //     public string sender;
    //     public uint sent_ledger;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("mail_type", mail_type),
    //                 SCUtility.FieldToSCMapEntry("message", message),
    //                 SCUtility.FieldToSCMapEntry("sender", sender),
    //                 SCUtility.FieldToSCMapEntry("sent_ledger", sent_ledger),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct User
    // {
    //     public uint current_lobby;
    //     public uint games_completed;
    //     public uint instruction;
    //     public long liveUntilLedgerSeq; // not serialized
    //     
    //     public override string ToString()
    //     {
    //         return JsonConvert.SerializeObject(this, Formatting.Indented);
    //     }
    // }
    // [Serializable]
    // public struct LobbyInfo
    // {
    //     public uint index;
    //     public SCVal.ScvAddress guest_address;
    //     public SCVal.ScvAddress host_address;
    //     public LobbyStatus status;
    //     public long liveUntilLedgerSeq; // not serialized
    //     
    //     public override string ToString()
    //     {
    //         var simplified = new
    //         {
    //             index,
    //             guest_address = Globals.AddressToString(guest_address),
    //             host_address = Globals.AddressToString(host_address),
    //             status = status.ToString(),
    //         };
    //         return JsonConvert.SerializeObject(simplified, Formatting.Indented);
    //     }
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("index", index),
    //                 SCUtility.FieldToSCMapEntry("guest_address", guest_address),
    //                 SCUtility.FieldToSCMapEntry("host_address", host_address),
    //                 SCUtility.FieldToSCMapEntry("status", status),
    //             }),
    //         };
    //     }
    // }
    // [Serializable]
    // public struct GameState : IScvMapCompatable
    // {
    //     public Phase phase;
    //     public UserState[] user_states;
    //     public long liveUntilLedgerSeq; // not serialized
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("phase", phase),
    //                 SCUtility.FieldToSCMapEntry("user_states", user_states),
    //             }),
    //         };
    //     }
    //     
    //     public UserState GetUserState(bool isClientHost)
    //     {
    //         int index = isClientHost ? 0 : 1;
    //         return user_states[index];
    //     }
    //
    //     public UserState GetOpponentUserState(bool isClientHost)
    //     {
    //         int index = isClientHost ? 1 : 0;
    //         return user_states[index];
    //     }
    // }
    //
    // [Serializable]
    // public struct Pos: IScvMapCompatable, IEquatable<Pos>
    // {
    //     public int x;
    //     public int y;
    //
    //     public Pos(Vector2Int vector)
    //     {
    //         x = vector.x;
    //         y = vector.y;
    //     }
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("x", x),
    //                 SCUtility.FieldToSCMapEntry("y", y),
    //             }),
    //         };
    //     }
    //
    //     public readonly Vector2Int ToVector2Int()
    //     {
    //         return new Vector2Int(x, y);
    //     }
    //
    //     public bool Equals(Pos other)
    //     {
    //         return x == other.x && y == other.y;
    //     }
    //     
    //     public bool Equals(Vector2Int other)
    //     {
    //         return x == other.x && y == other.y;
    //     }
    //
    //     public override bool Equals(object obj)
    //     {
    //         return obj is Pos other && Equals(other) || 
    //                obj is Vector2Int vector && Equals(vector);
    //     }
    //
    //     public override int GetHashCode()
    //     {
    //         return HashCode.Combine(x, y);
    //     }
    //
    //     public static bool operator ==(Pos left, Pos right)
    //     {
    //         return left.Equals(right);
    //     }
    //
    //     public static bool operator !=(Pos left, Pos right)
    //     {
    //         return !left.Equals(right);
    //     }
    // }
    //
    // [Serializable]
    // public struct UserState: IScvMapCompatable
    // {
    //     public PawnCommit[] setup;
    //     public byte[] setup_hash;
    //     public uint setup_hash_salt;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("setup", setup),
    //                 SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
    //                 SCUtility.FieldToSCMapEntry("setup_hash_salt", setup_hash_salt),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct MaxPawns : IScvMapCompatable
    // {
    //     public int max;
    //     public int rank;
    //
    //     public MaxPawns(SMaxPawnsPerRank maxPawns)
    //     {
    //         max = maxPawns.max;
    //         rank = (int)maxPawns.rank;
    //     }
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         SCVal.ScvMap scvMap = new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("max", max),
    //                 SCUtility.FieldToSCMapEntry("rank", rank),
    //             }),
    //         };
    //         return scvMap;
    //     }
    // }
    //
    // [Serializable]
    // public struct Mailbox : IScvMapCompatable
    // {
    //     public string lobby;
    //     public Mail[] mail;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         SCVal.ScvMap scvMap = new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("lobby", lobby),
    //                 SCUtility.FieldToSCMapEntry("mail", mail),
    //             }),
    //         };
    //         return scvMap;
    //     }
    // }
    //
    // [Serializable]
    // public struct ResolveEvent : IScvMapCompatable
    // {
    //     public string defender_pawn_id;
    //     public uint event_type;
    //     public Pos original_pos;
    //     public string pawn_id;
    //     public Pos target_pos;
    //     public uint team;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("defender_pawn_id", defender_pawn_id),
    //                 SCUtility.FieldToSCMapEntry("event_type", event_type),
    //                 SCUtility.FieldToSCMapEntry("original_pos", original_pos),
    //                 SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
    //                 SCUtility.FieldToSCMapEntry("target_pos", target_pos),
    //                 SCUtility.FieldToSCMapEntry("team", team),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct Tile: IScvMapCompatable
    // {
    //     public int auto_setup_zone;
    //     public bool is_passable;
    //     public Pos pos;
    //     public int setup_team;  // Team enum
    //
    //     public Tile(global::Tile tile)
    //     {
    //         auto_setup_zone = tile.autoSetupZone;
    //         is_passable = tile.isPassable;
    //         pos = new Pos(tile.pos);
    //         setup_team = (int)tile.setupTeam;
    //     }
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("auto_setup_zone", auto_setup_zone),
    //                 SCUtility.FieldToSCMapEntry("is_passable", is_passable),
    //                 SCUtility.FieldToSCMapEntry("pos", pos),
    //                 SCUtility.FieldToSCMapEntry("setup_team", setup_team),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct HiddenRank : IScvMapCompatable
    // {
    //     public Rank rank;
    //     public ulong salt;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("rank", rank),
    //                 SCUtility.FieldToSCMapEntry("salt", salt),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct PawnCommit : IScvMapCompatable
    // {
    //     public byte[] hidden_rank_hash;
    //     public uint pawn_id;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("hidden_rank_hash", hidden_rank_hash),
    //                 SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct Pawn : IScvMapCompatable
    // {
    //     public bool is_alive;
    //     public bool is_moved;
    //     public bool is_revealed;
    //     public string pawn_def_hash;
    //     public uint pawn_id;
    //     public Pos pos;
    //     public uint team; // Team enum
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("is_alive", is_alive),
    //                 SCUtility.FieldToSCMapEntry("is_moved", is_moved),
    //                 SCUtility.FieldToSCMapEntry("is_revealed", is_revealed),
    //                 SCUtility.FieldToSCMapEntry("pawn_def_hash", pawn_def_hash),
    //                 SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
    //                 SCUtility.FieldToSCMapEntry("pos", pos),
    //                 SCUtility.FieldToSCMapEntry("team", team),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct LobbyParameters : IScvMapCompatable
    // {
    //     public byte[] board_hash;
    //     public bool dev_mode;
    //     public uint host_team;
    //     public MaxRank[] max_ranks;
    //     public bool must_fill_all_tiles;
    //     public bool security_mode;
    //     public long liveUntilLedgerSeq; // not serialized
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("board_hash", board_hash),
    //                 SCUtility.FieldToSCMapEntry("dev_mode", dev_mode),
    //                 SCUtility.FieldToSCMapEntry("host_team", host_team),
    //                 SCUtility.FieldToSCMapEntry("max_ranks", max_ranks),
    //                 SCUtility.FieldToSCMapEntry("must_fill_all_tiles", must_fill_all_tiles),
    //                 SCUtility.FieldToSCMapEntry("security_mode", security_mode),
    //             }),
    //         };
    //     }
    //     
    //     public override string ToString()
    //     {
    //         var simplified = new
    //         {
    //             board_hash = BitConverter.ToString(board_hash).Replace("-", "").ToLowerInvariant(),
    //             dev_mode = dev_mode,
    //             host_team = host_team,
    //             max_ranks = max_ranks, // assumes MaxRank[] has a clean ToString() or JSON-friendly format
    //             must_fill_all_tiles = must_fill_all_tiles,
    //             security_mode = security_mode
    //         };
    //         return JsonConvert.SerializeObject(simplified, Formatting.Indented);
    //     }
    // }
    //
    // [Serializable]
    // public struct TurnMove : IScvMapCompatable
    // {
    //     public bool initialized;
    //     public string pawn_id;
    //     public Pos pos;
    //     public int turn;
    //     public string user_address;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("initialized", initialized),
    //                 SCUtility.FieldToSCMapEntry("pawn_id", pawn_id),
    //                 SCUtility.FieldToSCMapEntry("pos", pos),
    //                 SCUtility.FieldToSCMapEntry("turn", turn),
    //                 SCUtility.FieldToSCMapEntry("user_address", user_address),
    //             }),
    //         };
    //     }
    //
    //     public override string ToString()
    //     {
    //         return JsonUtility.ToJson(this).ToString();
    //     }
    // }
    //
    // [Serializable]
    // public struct Turn : IScvMapCompatable
    // {
    //     public ResolveEvent[] guest_events;
    //     public string guest_events_hash;
    //     public TurnMove guest_turn;
    //     public ResolveEvent[] host_events;
    //     public string host_events_hash;
    //     public TurnMove host_turn;
    //     public int turn;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("guest_events", guest_events),
    //                 SCUtility.FieldToSCMapEntry("guest_events_hash", guest_events_hash),
    //                 SCUtility.FieldToSCMapEntry("guest_turn", guest_turn),
    //                 SCUtility.FieldToSCMapEntry("host_events", host_events),
    //                 SCUtility.FieldToSCMapEntry("host_events_hash", host_events_hash),
    //                 SCUtility.FieldToSCMapEntry("host_turn", host_turn),
    //                 SCUtility.FieldToSCMapEntry("turn", turn),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct Lobby : IScvMapCompatable
    // {
    //     public uint game_end_state;
    //     public string guest_address;
    //     public UserState guest_state;
    //     public string host_address;
    //     public UserState host_state;
    //     public string index;
    //     public LobbyParameters parameters;
    //     public Pawn[] pawns;
    //     public uint phase; // Phase enum
    //     public Turn[] turns;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("game_end_state", game_end_state),
    //                 SCUtility.FieldToSCMapEntry("guest_address", guest_address),
    //                 SCUtility.FieldToSCMapEntry("guest_state", guest_state),
    //                 SCUtility.FieldToSCMapEntry("host_address", host_address),
    //                 SCUtility.FieldToSCMapEntry("host_state", host_state),
    //                 SCUtility.FieldToSCMapEntry("index", index),
    //                 SCUtility.FieldToSCMapEntry("parameters", parameters),
    //                 SCUtility.FieldToSCMapEntry("pawns", pawns),
    //                 SCUtility.FieldToSCMapEntry("phase", phase),
    //                 SCUtility.FieldToSCMapEntry("turns", turns),
    //             }),
    //         };
    //     }
    //     public Turn GetLatestTurn()
    //     {
    //         return turns.Last();
    //     }
    //     
    //     public Pawn GetPawnById(uint pawn_id)
    //     {
    //         return pawns.First(x => x.pawn_id == pawn_id);
    //     }
    //
    //     public Pawn? GetPawnByPosition(Vector2Int pos)
    //     {
    //         foreach (Pawn p in pawns)
    //         {
    //             if (p.pos == new Pos(pos))
    //             {
    //                 return p;
    //             }
    //         }
    //         return null;
    //     }
    // }
    //
    // [Serializable]
    // public struct ProveSetupReq : IScvMapCompatable
    // {
    //     
    //     public uint lobby_id;
    //     public ulong salt;
    //     public PawnCommit[] setup;
    //     
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
    //                 SCUtility.FieldToSCMapEntry("salt", salt),
    //                 SCUtility.FieldToSCMapEntry("setup", setup),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct MakeLobbyReq : IScvMapCompatable
    // {
    //     public uint lobby_id;
    //     public LobbyParameters parameters;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
    //                 SCUtility.FieldToSCMapEntry("parameters", parameters),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct JoinLobbyReq : IScvMapCompatable
    // {
    //     public uint lobby_id;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
    //             }),
    //         };
    //     }
    // }
    //
    // [Serializable]
    // public struct CommitSetupReq : IScvMapCompatable
    // {
    //     public uint lobby_id;
    //     public byte[] setup_hash;
    //
    //     public SCVal.ScvMap ToScvMap()
    //     {
    //         return new SCVal.ScvMap()
    //         {
    //             map = new SCMap(new[]
    //             {
    //                 SCUtility.FieldToSCMapEntry("lobby_id", lobby_id),
    //                 SCUtility.FieldToSCMapEntry("setup_hash", setup_hash),
    //             }),
    //         };
    //     }
    // }
    //
    //
    // public enum Phase : uint
    // {
    //     Setup = 0,
    //     Movement = 1,
    //     Completed = 2,
    // }
    //
    // public enum LobbyStatus : uint
    // {
    //     WaitingForPlayers = 0,
    //     GameInProgress = 1,
    //     HostWin = 2,
    //     GuestWin = 3,
    //     Draw = 4,
    //     Aborted = 5,
    // }
}
