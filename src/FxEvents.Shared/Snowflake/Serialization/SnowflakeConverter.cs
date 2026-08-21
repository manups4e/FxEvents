using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FxEvents.Shared.Snowflakes.Serialization
{

	public class SnowflakeConverter : JsonConverter<Snowflake>
	{
		public SnowflakeRepresentation Representation { get; set; }

		public override bool CanConvert(Type objectType) => objectType == typeof(Snowflake);

		public override Snowflake Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return Representation == SnowflakeRepresentation.UInt
				? new Snowflake((long)(reader.GetUInt32()))
				: new Snowflake(ulong.Parse(reader.GetString() ?? "0"));
		}

		public override void Write(Utf8JsonWriter writer, Snowflake value, JsonSerializerOptions options)
		{
			if (value is Snowflake snowflake)
			{
				if (Representation == SnowflakeRepresentation.UInt)
					writer.WriteNumberValue(snowflake.ToInt64());
				else
					writer.WriteStringValue(snowflake.ToString());
			}
		}
	}
}