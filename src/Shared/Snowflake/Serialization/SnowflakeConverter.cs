using Newtonsoft.Json;
using System;

namespace FxEvents.Shared.Snowflakes.Serialization
{

    public class SnowflakeConverter : JsonConverter
    {
        public SnowflakeRepresentation Representation { get; set; }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Snowflake snowflake)
            {
                if (Representation == SnowflakeRepresentation.UInt)
                    writer.WriteValue(snowflake.ToInt64());
                else
                    writer.WriteValue(snowflake.ToString());
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Representation == SnowflakeRepresentation.UInt
                ? new Snowflake((long)(reader.Value ?? 0))
                : new Snowflake(ulong.Parse((string)reader.Value ?? "0"));
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(Snowflake);
    }
}