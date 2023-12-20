using Newtonsoft.Json;
using System;
using System.Text;

namespace FxEvents.Shared.Serialization.Implementations
{
    public class JsonSerialization : ISerialization
    {
        public JsonSerialization()
        {
        }

        public void Serialize(Type type, object value, SerializationContext context)
        {
            context.Writer.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)));
        }

        public void Serialize<T>(T value, SerializationContext context)
        {
            Serialize(typeof(T), value, context);
        }

        public object Deserialize(Type type, SerializationContext context)
        {
            return JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(context.Reader.ReadBytes(context.Original!.Length)), type);
        }

        public T Deserialize<T>(SerializationContext context)
        {
            return (T)Deserialize(typeof(T), context);
        }
    }
}