using System;
using System.Globalization;
using System.Linq;
using System.Text;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;

namespace FxEvents.Shared
{
    public static class BinaryHelper
    {
        private static BinarySerialization _serialization = new();

        public static byte[] ToBytes<T>(this T obj)
        {
            using SerializationContext context = new("BinaryHelper", "ToBytes", _serialization);
            context.Serialize(typeof(T), obj);
            return context.GetData();
        }
        public static byte[] StringToBytes(this string str)
        {
            var arr = str.ToCharArray();
            if (arr[2] != '-' && arr[5] != '-') return default;
            return str.Split('-').Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray();
        }

        public static string BytesToString(this byte[] ba, bool separator = false, bool toLower = true)
        {
            string bytes;
            if(separator)
                bytes = BitConverter.ToString(ba);
            else
                bytes = BitConverter.ToString(ba).Replace("-", "");

            if(toLower)
                bytes = bytes.ToLower();
            return bytes;
        }

        public static T FromBytes<T>(this byte[] data)
        {
            using SerializationContext context = new(data.ToString(), "FromBytes", _serialization, data);
            return context.Deserialize<T>();
        }
    }
}