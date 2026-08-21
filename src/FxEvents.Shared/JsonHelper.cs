using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.Snowflakes.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace FxEvents.Shared
{
	public static class JsonHelper
	{
		public static readonly Dictionary<Type, Type> Substitutes = new Dictionary<Type, Type>();

		private static readonly SnowflakeConverter _snowflakeConverter = new SnowflakeConverter();

		public static readonly List<JsonConverter> Converters = new List<JsonConverter>
		{
			_snowflakeConverter
		};

		// Opzioni predefinite base
		public static readonly JsonSerializerOptions Empty = CreateDefaultOptions();

		// Opzioni che ignorano l'attributo [JsonIgnore]
		public static readonly JsonSerializerOptions IgnoreJsonIgnoreAttributes = new JsonSerializerOptions
		{
			TypeInfoResolver = new IgnoreJsonAttributesResolver()
		};

		// Opzioni con naming convention snake_case e rimozione dei valori null
		public static readonly JsonSerializerOptions LowerCaseSettings = CreateLowerCaseOptions();

		private static JsonSerializerOptions CreateDefaultOptions()
		{
			var options = new JsonSerializerOptions();
			foreach (var converter in Converters)
			{
				options.Converters.Add(converter);
			}
			return options;
		}

		private static JsonSerializerOptions CreateLowerCaseOptions()
		{
			var options = CreateDefaultOptions();
			options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
			options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			return options;
		}

		public static string ToJson(this object value, bool pretty = false, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
			JsonSerializerOptions settings = null)
		{
			var options = settings != null ? new JsonSerializerOptions(settings) : CreateDefaultOptions();
			if (pretty)
			{
				options.WriteIndented = true;
			}

			return (string)InvokeWithRepresentation(
				() => JsonSerializer.Serialize(value, options), representation);
		}

		public static T FromJson<T>(this string serialized, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
			JsonSerializerOptions settings = null) => (T)FromJsonInternal(serialized, typeof(T), out _, representation, settings);

		public static object FromJson(this string serialized, Type type, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
			JsonSerializerOptions settings = null) => FromJsonInternal(serialized, type, out _, representation, settings);

		public static T FromJson<T>(this string serialized, out bool result, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
			JsonSerializerOptions settings = null)
		{
			object value = FromJsonInternal(serialized, typeof(T), out bool transient, representation, settings);

			result = transient;
			return (T)value;
		}

		private static object FromJsonInternal(string serialized, Type type, out bool result, SnowflakeRepresentation representation,
			JsonSerializerOptions settings)
		{
			try
			{
				object deserialized = InvokeWithRepresentation(
					() => JsonSerializer.Deserialize(serialized, type, settings ?? Empty),
					representation, false);

				result = true;
				return deserialized;
			}
			catch (Exception)
			{
				result = false;
				throw;
			}
		}

		private static object InvokeWithRepresentation(Func<object> func, SnowflakeRepresentation representation, bool suppressErrors = true)
		{
			SnowflakeRepresentation transient = _snowflakeConverter.Representation;
			_snowflakeConverter.Representation = representation;

			try
			{
				return func.Invoke();
			}
			catch (Exception)
			{
				if (!suppressErrors)
					throw;
			}
			finally
			{
				_snowflakeConverter.Representation = transient;
			}

			return null;
		}
	}

	/// <summary>
	/// Custom TypeInfoResolver per riattivare la serializzazione delle proprietà marcate con [JsonIgnore]
	/// </summary>
	internal class IgnoreJsonAttributesResolver : DefaultJsonTypeInfoResolver
	{
		public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
		{
			JsonTypeInfo typeInfo = base.GetTypeInfo(type, options);

			if (typeInfo.Kind == JsonTypeInfoKind.Object)
			{
				foreach (JsonPropertyInfo property in typeInfo.Properties)
				{
					// Ignora la condizione di JsonIgnore riattivando la proprietà
					property.ShouldSerialize = null;
				}
			}

			return typeInfo;
		}
	}
}