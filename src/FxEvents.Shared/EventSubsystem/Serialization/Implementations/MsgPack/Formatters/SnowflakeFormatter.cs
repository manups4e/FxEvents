using MessagePack;
using MessagePack.Formatters;
using FxEvents.Shared.Snowflakes;

namespace FxEvents.Shared.Serialization.Formatters
{
	public class SnowflakeFormatter : IMessagePackFormatter<Snowflake>
	{
		public static readonly SnowflakeFormatter Instance = new();

		public void Serialize(ref MessagePackWriter writer, Snowflake value, MessagePackSerializerOptions options)
		{
			writer.Write(value.Value);
		}

		public Snowflake Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			return new Snowflake(reader.ReadUInt64());
		}
	}
}