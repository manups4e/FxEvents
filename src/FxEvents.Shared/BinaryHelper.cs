using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using System;
using System.Globalization;
using System.Linq;

namespace FxEvents.Shared
{
    public static class BinaryHelper
    {
        private static MsgPackSerialization msgpackSerialization = new();

        public static byte[] ToBytes<T>(this T obj)
        {
            using SerializationContext context = new("BinaryHelper", "ToBytes", msgpackSerialization);
            context.Serialize(typeof(T), obj);
            return context.GetData();
        }
        public static byte[] StringToBytes(this string str)
        {
            if (string.IsNullOrEmpty(str)) return new byte[0];

            if (str.Contains("-"))
            {
                string[] parts = str.Split('-');
                byte[] result = new byte[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    result[i] = byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return result;
            }
            else
            {
                int len = str.Length;
                byte[] bytes = new byte[len / 2];
                for (int i = 0; i < len; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(str.Substring(i, 2), 16);
                }
                return bytes;
            }
        }

        public static string BytesToString(this byte[] ba, bool separator = false, bool toLower = true)
        {
            if (ba == null || ba.Length == 0) return string.Empty;

            if (separator)
            {
                string hex = BitConverter.ToString(ba);
                return toLower ? hex.ToLowerInvariant() : hex;
            }

            char[] c = new char[ba.Length * 2];
            string hexChars = toLower ? "0123456789abcdef" : "0123456789ABCDEF";
            for (int i = 0; i < ba.Length; i++)
            {
                int b = ba[i];
                c[i * 2] = hexChars[b >> 4];
                c[i * 2 + 1] = hexChars[b & 0xF];
            }
            return new string(c);
        }

        public static T FromBytes<T>(this byte[] data)
        {
            using SerializationContext context = new(data.ToString(), "FromBytes", msgpackSerialization, data);
            return context.Deserialize<T>();
        }

        public static ulong ToUInt64(this byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > sizeof(ulong))
                throw new ArgumentException("Must be 8 elements or fewer", nameof(bytes));

            ulong result = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                result |= (ulong)bytes[i] << (i * 8);
            }
            return result;
        }

        public static byte[] FromUInt64(this ulong num)
        {
            byte[] buffer = new byte[sizeof(ulong)];
            for (int i = 0; i < sizeof(ulong); i++)
            {
                buffer[i] = (byte)(num >> (i * 8));
            }
            return buffer;
        }
    }
}