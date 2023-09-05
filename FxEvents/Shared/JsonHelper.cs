using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.Snowflakes.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace FxEvents.Shared
{
    public static class JsonHelper
    {
        public static readonly Dictionary<Type, Type> Substitutes = new Dictionary<Type, Type>();
        private static SnowflakeConverter _snowflakeConverter = new SnowflakeConverter();

        public static readonly List<JsonConverter> Converters = new List<JsonConverter>
        {
            _snowflakeConverter
        };

        public static readonly JsonSerializerSettings Empty = new()
        {
            Converters = Converters,
            ContractResolver = new ContractResolver()
        };

        public static readonly JsonSerializerSettings IgnoreJsonIgnoreAttributes = new()
        {
            ContractResolver = new IgnoreJsonAttributesResolver()
        };

        public static readonly JsonSerializerSettings LowerCaseSettings = new()
        {
            Converters = Converters,
            ContractResolver = new ContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string ToJson(this object value, bool pretty = false, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
            JsonSerializerSettings settings = null)
        {
            return (string)InvokeWithRepresentation(
                () => JsonConvert.SerializeObject(value, pretty ? Formatting.Indented : Formatting.None, settings ?? Empty), representation);
        }

        public static T FromJson<T>(this string serialized, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
            JsonSerializerSettings settings = null) => (T)FromJsonInternal(serialized, typeof(T), out _, representation, settings);

        public static object FromJson(this string serialized, Type type, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
            JsonSerializerSettings settings = null) => FromJsonInternal(serialized, type, out _, representation, settings);

        public static T FromJson<T>(this string serialized, out bool result, SnowflakeRepresentation representation = SnowflakeRepresentation.String,
            JsonSerializerSettings settings = null)
        {
            object value = FromJsonInternal(serialized, typeof(T), out bool transient, representation, settings);

            result = transient;
            return (T)value;
        }

        private static object FromJsonInternal(string serialized, Type type, out bool result, SnowflakeRepresentation representation,
            JsonSerializerSettings settings)
        {
            try
            {
                object deserialized = InvokeWithRepresentation(() => JsonConvert.DeserializeObject(serialized, type, settings ?? Empty),
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

            _snowflakeConverter.Representation = transient;

            return null;
        }
    }

    internal class IgnoreJsonAttributesResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
            foreach (JsonProperty prop in props)
            {
                prop.Ignored = false;   // Ignore [JsonIgnore]
                                        //prop.Converter = null;  // Ignore [JsonConverter]
                                        //prop.PropertyName = prop.UnderlyingName;  // Use original property name instead of [JsonProperty] name
            }
            return props;
        }
    }

}