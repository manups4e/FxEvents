using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization
{
    /*
     * CODE TAKEN FROM FIVEM'S MONO V2 MSGPACK PROJECT.
     * THANKS THORIUM FOR YOUR IMMENSE HELP AND DEDICATION 
     * TOWARDS OUR COMMUNITY. YOU ARE A TRUE HERO AND A BELOVED FRIEND!
    */
    internal static class TypeConvert
    {
        internal static object GetNewHolder(SerializationContext context, Type type)
        {
            MemoryStream _memory = context.GetMemory();
            _memory.Position = 0;


            TypeCode typeCode = Type.GetTypeCode(type);
            return typeCode switch
            {
                TypeCode.Char or TypeCode.String => DeserializeAsString(_memory),
                TypeCode.Byte => (byte)DeserializeAsInt32(_memory),
                TypeCode.SByte => (sbyte)DeserializeAsInt32(_memory),
                TypeCode.Int16 => DeserializeAsInt32(_memory),
                TypeCode.Int32 => DeserializeAsInt32(_memory),
                TypeCode.Int64 => DeserializeAsInt64(_memory),
                TypeCode.UInt16 or TypeCode.UInt32 => DeserializeAsUInt32(_memory),
                TypeCode.UInt64 => DeserializeAsUInt64(_memory),
                TypeCode.Boolean => DeserializeAsBool(_memory),
                TypeCode.Decimal or TypeCode.Double => DeserializeAsReal64(_memory),
                TypeCode.Single => DeserializeAsReal32(_memory),
                _ => context.Deserialize(type),
            };
        }

        private static byte ReadByte(MemoryStream data)
        {
            return (byte)data.ReadByte();
        }

        internal static MsgPackCode ReadType(ref MemoryStream data)
        {
            var code = (MsgPackCode)ReadByte(data);
            return code;
        }

        #region Basic types

        internal static bool DeserializeAsBool(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return type != 0;
            else if (type <= MsgPackCode.MaximumFixedRaw) // fixstr
                return ReadStringAsTrueish(data, (uint)type % 32u);
            else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                return true; // is always != 0

            return type switch
            {
                MsgPackCode.NilValue or MsgPackCode.FalseValue => false,
                MsgPackCode.TrueValue => true,
                MsgPackCode.SignedInt8 or MsgPackCode.UnsignedInt8 => AdvancePointer(ref data, 1) != 0,
                MsgPackCode.SignedInt16 or MsgPackCode.UnsignedInt16 => AdvancePointer(ref data, 2) != 0,
                MsgPackCode.SignedInt32 or MsgPackCode.UnsignedInt32 or MsgPackCode.Real32 => AdvancePointer(ref data, 4) != 0,
                MsgPackCode.SignedInt64 or MsgPackCode.UnsignedInt64 or MsgPackCode.Real64 => AdvancePointer(ref data, 8) != 0,
                MsgPackCode.Str8 => ReadStringAsTrueish(data, ReadUInt8(data)),
                MsgPackCode.Raw16 => ReadStringAsTrueish(data, ReadUInt16(data)),
                MsgPackCode.Raw32 => ReadStringAsTrueish(data, ReadUInt32(data)),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(bool)}"),
            };
        }

        internal static uint DeserializeAsUInt32(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (uint)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return uint.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((uint)(sbyte)type);
            }

            switch (type)
            {
                case MsgPackCode.NilValue: // null
                case MsgPackCode.FalseValue: return (uint)0;
                case MsgPackCode.TrueValue: return (uint)1;
                case MsgPackCode.Real32: return (uint)ReadSingle(data);
                case MsgPackCode.Real64: return (uint)ReadDouble(data);
                case MsgPackCode.UnsignedInt8: return (uint)ReadUInt8(data);
                case MsgPackCode.UnsignedInt16: return (uint)ReadUInt16(data);
                case MsgPackCode.UnsignedInt32: return (uint)ReadUInt32(data);
                case MsgPackCode.UnsignedInt64: return (uint)ReadUInt64(data);
                case MsgPackCode.SignedInt8: return (uint)ReadInt8(data);
                case MsgPackCode.SignedInt16: return (uint)ReadInt16(data);
                case MsgPackCode.SignedInt32: return (uint)ReadInt32(data);
                case MsgPackCode.SignedInt64: return (uint)ReadInt64(data);
                case MsgPackCode.Str8: return uint.Parse(ReadString(data, ReadUInt8(data)));
                case MsgPackCode.Raw16: return uint.Parse(ReadString(data, ReadUInt16(data)));
                case MsgPackCode.Raw32: return uint.Parse(ReadString(data, ReadUInt32(data)));
            }

            throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(uint)}");
        }

        internal static ulong DeserializeAsUInt64(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (ulong)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return ulong.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((ulong)(sbyte)type);
            }

            switch (type)
            {
                case MsgPackCode.NilValue: // null
                case MsgPackCode.FalseValue: return (ulong)0;
                case MsgPackCode.TrueValue: return (ulong)1;
                case MsgPackCode.Real32: return (ulong)ReadSingle(data);
                case MsgPackCode.Real64: return (ulong)ReadDouble(data);
                case MsgPackCode.UnsignedInt8: return (ulong)ReadUInt8(data);
                case MsgPackCode.UnsignedInt16: return (ulong)ReadUInt16(data);
                case MsgPackCode.UnsignedInt32: return (ulong)ReadUInt32(data);
                case MsgPackCode.UnsignedInt64: return (ulong)ReadUInt64(data);
                case MsgPackCode.SignedInt8: return (ulong)ReadInt8(data);
                case MsgPackCode.SignedInt16: return (ulong)ReadInt16(data);
                case MsgPackCode.SignedInt32: return (ulong)ReadInt32(data);
                case MsgPackCode.SignedInt64: return (ulong)ReadInt64(data);
                case MsgPackCode.Str8: return ulong.Parse(ReadString(data, ReadUInt8(data)));
                case MsgPackCode.Raw16: return ulong.Parse(ReadString(data, ReadUInt16(data)));
                case MsgPackCode.Raw32: return ulong.Parse(ReadString(data, ReadUInt32(data)));
            }

            throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(ulong)}");
        }

        internal static int DeserializeAsInt32(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (int)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return int.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((int)(sbyte)type);
            }

            return type switch
            {
                MsgPackCode.NilValue or MsgPackCode.FalseValue => (int)0,
                MsgPackCode.TrueValue => (int)1,
                MsgPackCode.Real32 => (int)ReadSingle(data),
                MsgPackCode.Real64 => (int)ReadDouble(data),
                MsgPackCode.UnsignedInt8 => (int)ReadUInt8(data),
                MsgPackCode.UnsignedInt16 => (int)ReadUInt16(data),
                MsgPackCode.UnsignedInt32 => (int)ReadUInt32(data),
                MsgPackCode.UnsignedInt64 => (int)ReadUInt64(data),
                MsgPackCode.SignedInt8 => (int)ReadInt8(data),
                MsgPackCode.SignedInt16 => (int)ReadInt16(data),
                MsgPackCode.SignedInt32 => (int)ReadInt32(data),
                MsgPackCode.SignedInt64 => (int)ReadInt64(data),
                MsgPackCode.Str8 => int.Parse(ReadString(data, ReadUInt8(data))),
                MsgPackCode.Raw16 => int.Parse(ReadString(data, ReadUInt16(data))),
                MsgPackCode.Raw32 => int.Parse(ReadString(data, ReadUInt32(data))),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(int)}"),
            };
        }

        internal static long DeserializeAsInt64(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (long)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return long.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((long)(sbyte)type);
            }

            return type switch
            {
                // null
                MsgPackCode.NilValue or MsgPackCode.FalseValue => (long)0,
                MsgPackCode.TrueValue => (long)1,
                MsgPackCode.Real32 => (long)ReadSingle(data),
                MsgPackCode.Real64 => (long)ReadDouble(data),
                MsgPackCode.UnsignedInt8 => (long)ReadUInt8(data),
                MsgPackCode.UnsignedInt16 => (long)ReadUInt16(data),
                MsgPackCode.UnsignedInt32 => (long)ReadUInt32(data),
                MsgPackCode.UnsignedInt64 => (long)ReadUInt64(data),
                MsgPackCode.SignedInt8 => (long)ReadInt8(data),
                MsgPackCode.SignedInt16 => (long)ReadInt16(data),
                MsgPackCode.SignedInt32 => (long)ReadInt32(data),
                MsgPackCode.SignedInt64 => (long)ReadInt64(data),
                MsgPackCode.Str8 => long.Parse(ReadString(data, ReadUInt8(data))),
                MsgPackCode.Raw16 => long.Parse(ReadString(data, ReadUInt16(data))),
                MsgPackCode.Raw32 => long.Parse(ReadString(data, ReadUInt32(data))),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(long)}"),
            };
        }

        internal static float DeserializeAsReal32(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (float)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return float.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((float)(sbyte)type);
            }

            return type switch
            {
                // null
                MsgPackCode.NilValue or MsgPackCode.FalseValue => (float)0,
                MsgPackCode.TrueValue => (float)1,
                MsgPackCode.Real32 => (float)ReadSingle(data),
                MsgPackCode.Real64 => (float)ReadDouble(data),
                MsgPackCode.UnsignedInt8 => (float)ReadUInt8(data),
                MsgPackCode.UnsignedInt16 => (float)ReadUInt16(data),
                MsgPackCode.UnsignedInt32 => (float)ReadUInt32(data),
                MsgPackCode.UnsignedInt64 => (float)ReadUInt64(data),
                MsgPackCode.SignedInt8 => (float)ReadInt8(data),
                MsgPackCode.SignedInt16 => (float)ReadInt16(data),
                MsgPackCode.SignedInt32 => (float)ReadInt32(data),
                MsgPackCode.SignedInt64 => (float)ReadInt64(data),
                MsgPackCode.Str8 => float.Parse(ReadString(data, ReadUInt8(data))),
                MsgPackCode.Raw16 => float.Parse(ReadString(data, ReadUInt16(data))),
                MsgPackCode.Raw32 => float.Parse(ReadString(data, ReadUInt32(data))),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(float)}"),
            };
        }

        internal static double DeserializeAsReal64(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.FixIntPositiveMax) // positive fixint
                return (double)type;
            else if (type >= MsgPackCode.MinimumFixedRaw) // fixstr
            {
                if (type <= MsgPackCode.MaximumFixedRaw)
                    return double.Parse(ReadString(data, (uint)type - (uint)MsgPackCode.MinimumFixedRaw));
                else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
                    return unchecked((double)(sbyte)type);
            }

            return type switch
            {
                // null
                MsgPackCode.NilValue or MsgPackCode.FalseValue => (double)0,
                MsgPackCode.TrueValue => (double)1,
                MsgPackCode.Real32 => (double)ReadSingle(data),
                MsgPackCode.Real64 => (double)ReadDouble(data),
                MsgPackCode.UnsignedInt8 => (double)ReadUInt8(data),
                MsgPackCode.UnsignedInt16 => (double)ReadUInt16(data),
                MsgPackCode.UnsignedInt32 => (double)ReadUInt32(data),
                MsgPackCode.UnsignedInt64 => (double)ReadUInt64(data),
                MsgPackCode.SignedInt8 => (double)ReadInt8(data),
                MsgPackCode.SignedInt16 => (double)ReadInt16(data),
                MsgPackCode.SignedInt32 => (double)ReadInt32(data),
                MsgPackCode.SignedInt64 => (double)ReadInt64(data),
                MsgPackCode.Str8 => double.Parse(ReadString(data, ReadUInt8(data))),
                MsgPackCode.Raw16 => double.Parse(ReadString(data, ReadUInt16(data))),
                MsgPackCode.Raw32 => double.Parse(ReadString(data, ReadUInt32(data))),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(double)}"),
            };
        }

        internal static string DeserializeAsString(MemoryStream data)
        {
            MsgPackCode type = ReadType(ref data);
            if (type <= MsgPackCode.MaximumFixedRaw)
            {
                if (type <= MsgPackCode.FixIntPositiveMax)
                    return ((byte)type).ToString();
                else if (type <= MsgPackCode.MaximumFixedMap)
                    return "Dictionary<object, object>";
                else if (type <= MsgPackCode.MaximumFixedArray)
                    return "object[]";
                else
                    return ReadString(data, (byte)type % 32u);
            }
            else if (type >= MsgPackCode.FixIntNegativeMin) // anything at the end of our byte
            {
                return unchecked((sbyte)type).ToString();
            }

            return type switch
            {
                MsgPackCode.NilValue => null,
                MsgPackCode.FalseValue => "false",
                MsgPackCode.TrueValue => "true",
                MsgPackCode.Real32 => ReadSingle(data).ToString(),
                MsgPackCode.Real64 => ReadDouble(data).ToString(),
                MsgPackCode.UnsignedInt8 => ReadUInt8(data).ToString(),
                MsgPackCode.UnsignedInt16 => ReadUInt16(data).ToString(),
                MsgPackCode.UnsignedInt32 => ReadUInt32(data).ToString(),
                MsgPackCode.UnsignedInt64 => ReadUInt64(data).ToString(),
                MsgPackCode.SignedInt8 => ReadInt8(data).ToString(),
                MsgPackCode.SignedInt16 => ReadInt16(data).ToString(),
                MsgPackCode.SignedInt32 => ReadInt32(data).ToString(),
                MsgPackCode.SignedInt64 => ReadInt64(data).ToString(),
                MsgPackCode.Str8 => ReadString(data, ReadUInt8(data)),
                MsgPackCode.Raw16 => ReadString(data, ReadUInt16(data)),
                MsgPackCode.Raw32 => ReadString(data, ReadUInt32(data)),
                MsgPackCode.Array16 => "object[]",
                MsgPackCode.Array32 => "object[]",
                MsgPackCode.Map16 => nameof(Dictionary<object, object>),
                MsgPackCode.Map32 => nameof(Dictionary<object, object>),
                _ => throw new InvalidCastException($"MsgPack type {type} could not be deserialized into type {typeof(string)}"),
            };
        }
        #endregion

        #region Methods

        internal static string ReadString(MemoryStream data, uint length)
        {
            AdvancePointer(ref data, length);
            using SerializationContext context = new("string des", $"exactly for string", new MsgPackSerialization(), data.ToArray());
            return context.Deserialize(typeof(string)).ToString();
        }


        internal static bool ReadStringAsTrueish(MemoryStream data, uint amount)
        {
            AdvancePointer(ref data, amount);
            var buffer = data.ToArray();

            switch (buffer.Length)
            {
                case 1:
                    return !(buffer[0] == '0');
                case 4 when buffer[0] == 't' && buffer[1] == 'r' && buffer[2] == 'u' && buffer[3] == 'e':
                    return true;
                case 5 when buffer[0] == 'f' && buffer[1] == 'a' && buffer[2] == 'l' && buffer[3] == 's' && buffer[4] == 'e':
                    return false;
                default:
                    string val = ReadString(data, (uint)data.Length - 1);
                    if (int.TryParse(val, out int intval))
                        return intval != 0;
                    return false;
            }
        }

        private static long AdvancePointer(ref MemoryStream array, uint amount)
        {
            if (amount > (uint)array.Length)
            {
                throw new ArgumentException("Not enough bytes left in the array to advance by the requested amount.");
            }

            array.Position += amount;
            return array.Position;
        }

        public static float ReadSingle(MemoryStream data)
        {
            if (data.Length < sizeof(float))
            {
                throw new ArgumentException("Input byte array is too short to contain a float.");
            }

            byte[] buffer = new byte[sizeof(float)];
            data.Read(buffer, 0, buffer.Length);
            if (BitConverter.IsLittleEndian)
                buffer = buffer.Reverse().ToArray();
            return BitConverter.ToSingle(buffer, 0);
        }

        public static double ReadDouble(MemoryStream data)
        {
            if (data.Length < sizeof(double))
            {
                throw new ArgumentException("Input byte array is too short to contain a double.");
            }

            double result = (double)ReadSingle(data);
            return result;
        }

        public static byte ReadUInt8(MemoryStream data)
        {
            if (data.Length < sizeof(byte))
            {
                throw new ArgumentException("Input byte array is too short to contain a byte.");
            }

            return ReadByte(data);
        }

        public static ushort ReadUInt16(MemoryStream data)
        {
            if (data.Length < sizeof(ushort))
            {
                throw new ArgumentException("Input byte array is too short to contain a ushort.");
            }

            BinaryReader reader = new BinaryReader(data, Encoding.UTF8); // Specify encoding if needed
            ushort v = reader.ReadUInt16(); // Read a 16-bit unsigned integer
            if (BitConverter.IsLittleEndian)
                v = unchecked((ushort)((v >> 8) | (v << 8))); // swap adjacent 8-bit blocks
            return v;
        }

        public static uint ReadUInt32(MemoryStream data)
        {
            if (data.Length < sizeof(uint))
            {
                throw new ArgumentException("Input byte array is too short to contain a uint.");
            }

            BinaryReader reader = new BinaryReader(data, Encoding.UTF8); // Specify encoding if needed
            uint v = reader.ReadUInt32(); // Read a 16-bit unsigned integer
            if (BitConverter.IsLittleEndian)
            {
                v = unchecked((v >> 16) | (v << 16)); // swap adjacent 16-bit blocks
                v = unchecked(((v & 0xFF00FF00u) >> 8) | ((v & 0x00FF00FFu) << 8)); // swap adjacent 8-bit blocks
            }
            return v;
        }

        public static ulong ReadUInt64(MemoryStream data)
        {
            if (data.Length < sizeof(ulong))
            {
                throw new ArgumentException("Input byte array is too short to contain a ulong.");
            }

            BinaryReader reader = new BinaryReader(data, Encoding.UTF8); // Specify encoding if needed
            ulong v = reader.ReadUInt64(); // Read a 16-bit unsigned integer
            if (BitConverter.IsLittleEndian)
            {
                v = unchecked((v >> 32) | (v << 32)); // swap adjacent 32-bit blocks
                v = unchecked(((v & 0xFFFF0000FFFF0000u) >> 16) | ((v & 0x0000FFFF0000FFFFu) << 16)); // swap adjacent 16-bit blocks
                v = unchecked(((v & 0xFF00FF00FF00FF00u) >> 8) | ((v & 0x00FF00FF00FF00FFu) << 8)); // swap adjacent 8-bit blocks
            }
            return v;
        }

        public static sbyte ReadInt8(MemoryStream data) => (sbyte)ReadUInt8(data);

        public static short ReadInt16(MemoryStream data) => (short)ReadUInt16(data);

        public static int ReadInt32(MemoryStream data) => (int)ReadUInt32(data);

        public static long ReadInt64(MemoryStream data) => (long)ReadUInt64(data);


        #endregion

        #region OLD METHOD
        internal static object GetHolder(MessagePackObject msgpkObj, Type type)
        {
            object obj = msgpkObj.ToObject();
            TypeCode typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.String:
                    if (msgpkObj.IsNil)
                        return string.Empty;
                    return obj as string ?? (type.IsSimpleType() ? obj.ToString() : string.Empty);
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    if (obj is IConvertible convertible)
                    {
                        try
                        {
                            return Convert.ChangeType(convertible, type);
                        }
                        catch (InvalidCastException)
                        {
                            return GetDefaultForType(type);
                        }
                    }
                    else
                        return GetDefaultForType(type);
                case TypeCode.Boolean:
                    bool booleanValue;
                    if (bool.TryParse(obj.ToString(), out booleanValue))
                        return booleanValue;
                    else
                        return false;
                case TypeCode.Char:
                    char charValue;
                    if (char.TryParse(obj.ToString(), out charValue))
                        return charValue;
                    else
                        return '\0';
                case TypeCode.Decimal:
                    decimal decimalValue;
                    if (decimal.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue))
                        return decimalValue;
                    else
                        return 0M;
                case TypeCode.Single:
                    float floatValue;
                    if (float.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out floatValue))
                        return floatValue;
                    else
                        return 0F;
                case TypeCode.Double:
                    double doubleValue;
                    if (double.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                        return doubleValue;
                    else
                        return 0D;
                case TypeCode.DBNull:
                case TypeCode.DateTime:
                    return obj;
                default:
                    return GetDefaultForType(type);
            }
        }
        #endregion

        private static object GetDefaultForType(Type type)
        {
            // Determine the default value for the given type
            if (type == typeof(string)) return string.Empty;
            if (type == typeof(byte)) return 0;
            if (type == typeof(sbyte)) return 0;
            if (type == typeof(bool)) return false;
            if (type == typeof(char)) return '\0';
            if (type == typeof(DateTime)) return DateTime.MinValue;
            if (type == typeof(decimal)) return 0M;
            if (type == typeof(float)) return 0F;
            if (type == typeof(double)) return 0D;
            if (type == typeof(short)) return 0;
            if (type == typeof(int)) return 0;
            if (type == typeof(long)) return 0L;
            if (type == typeof(ushort)) return 0U;
            if (type == typeof(uint)) return 0U;
            if (type == typeof(ulong)) return 0UL;
            return null; // Fallback to null for unsupported types
        }
    }
}
