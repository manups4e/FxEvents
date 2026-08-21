using System;
using System.Text.Json;

namespace FxEvents.Shared.Serialization.Implementations
{
	public class JsonSerialization : ISerialization
	{
		private readonly JsonSerializerOptions _options;

		public JsonSerialization(JsonSerializerOptions options = null)
		{
			_options = options ?? JsonHelper.Empty;
		}

		public void Serialize(Type type, object value, SerializationContext context)
		{
			byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
			context.Writer.Write(utf8Bytes);
		}

		public void Serialize<T>(T value, SerializationContext context)
		{
			Serialize(typeof(T), value, context);
		}

		public object Deserialize(Type type, SerializationContext context)
		{
			byte[] bytes = context.Reader.ReadBytes(context.Original!.Length);
			return JsonSerializer.Deserialize(bytes, type, _options);
		}

		public T Deserialize<T>(SerializationContext context)
		{
			byte[] bytes = context.Reader.ReadBytes(context.Original!.Length);
			return JsonSerializer.Deserialize<T>(bytes, _options);
		}
	}
}