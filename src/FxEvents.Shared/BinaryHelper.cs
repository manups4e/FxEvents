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
            char[] arr = str.ToCharArray();
            if (arr[2] != '-' && arr[5] != '-')
                return Enumerable.Range(0, str.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(str.Substring(x, 2), 16)).ToArray();
            return str.Split('-').Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray();
        }

        public static string BytesToString(this byte[] ba, bool separator = false, bool toLower = true)
        {
            string bytes;
            if (separator)
                bytes = BitConverter.ToString(ba);
            else
                bytes = BitConverter.ToString(ba).Replace("-", "");

            if (toLower)
                bytes = bytes.ToLower();
            return bytes;
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