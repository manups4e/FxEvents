using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FxEvents.Shared
{
	public class TypeConverter : JsonConverterFactory
	{
		public Type DesiredType { get; }

		public TypeConverter(Type desiredType)
		{
			DesiredType = desiredType;
		}

		public override bool CanConvert(Type typeToConvert) => typeToConvert == DesiredType;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			// Crea opzioni derivate senza questo converter per evitare la ricorsione
			JsonSerializerOptions innerOptions = new JsonSerializerOptions(options);
			for (int i = innerOptions.Converters.Count - 1; i >= 0; i--)
			{
				if (innerOptions.Converters[i] is TypeConverter tc && tc.DesiredType == DesiredType)
				{
					innerOptions.Converters.RemoveAt(i);
				}
			}

			Type converterType = typeof(TypeConverterInner<>).MakeGenericType(DesiredType);
			return (JsonConverter)Activator.CreateInstance(converterType, innerOptions);
		}

		private class TypeConverterInner<T> : JsonConverter<T>
		{
			private readonly JsonSerializerOptions _options;

			public TypeConverterInner(JsonSerializerOptions options)
			{
				_options = options;
			}

			public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				return JsonSerializer.Deserialize<T>(ref reader, _options);
			}

			public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
			{
				throw new NotImplementedException();
			}
		}
	}
}