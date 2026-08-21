using FxEvents.Shared.EventSubsystem.Serialization;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FxEvents.Shared.Serialization.Implementations
{
	public class MsgPackSerialization : ISerialization
	{
		private readonly Log logger = new();
		private static readonly MessagePackSerializerOptions DefaultOptions = MessagePackSerializerOptions.Standard
			.WithResolver(StandardResolver.Instance);

		private static bool IsTuple(Type t) => t.Name.StartsWith("Tuple");

		#region Serialization
		public void Serialize(Type type, object value, SerializationContext context)
		{
			if (IsTuple(type))
			{
				logger.Warning("Using Tuple is not advised due to differences between client and server environments. Consider using ValueTuple instead.");
				SerializeTuple(type, value, context);
				return;
			}

			SerializeObject(type, value, context);
		}

		private void SerializeTuple(Type type, object value, SerializationContext context)
		{
			PropertyInfo[] properties = value.GetType().GetProperties();

			foreach (PropertyInfo property in properties)
			{
				object? propertyValue = property.GetValue(value, null);
				Serialize(propertyValue?.GetType() ?? typeof(object), propertyValue, context);
			}
		}

		private void SerializeObject(Type type, object value, SerializationContext context)
		{
			MessagePackSerializer.Serialize(type, context.Writer.BaseStream, value, DefaultOptions);
		}

		public void Serialize<T>(T value, SerializationContext context)
		{
			Serialize(typeof(T), value, context);
		}
		#endregion

		#region Deserialization
		public object Deserialize(Type type, SerializationContext context)
		{
			return MessagePackSerializer.Deserialize(type, context.Reader.BaseStream, DefaultOptions);
		}

		public T Deserialize<T>(SerializationContext context) => Deserialize<T>(typeof(T), context);

		public T Deserialize<T>(Type type, SerializationContext context)
		{
			if (IsTuple(type))
			{
				logger.Warning("Using Tuple is not advised due to differences between client and server environments. Consider using ValueTuple instead.");
				return DeserializeTuple<T>(type, context);
			}

			return DeserializeObject<T>(type, context);
		}

		private T DeserializeTuple<T>(Type type, SerializationContext context)
		{
			Type[] generics = type.GetGenericArguments();
			ConstructorInfo constructor = type.GetConstructor(generics) ??
								throw new SerializationException(context, type,
									$"Could not find suitable constructor for type: {type.Name}");

			List<object?> parameters = new(generics.Length);

			foreach (Type generic in generics)
			{
				object entry = Deserialize(generic, context);
				parameters.Add(entry);
			}

			return (T)constructor.Invoke(parameters.ToArray());
		}

		private T DeserializeObject<T>(Type type, SerializationContext context)
		{
			if (TypeCache<T>.IsSimpleType)
				return (T)TypeConvert.GetNewHolder(context, type);

			return MessagePackSerializer.Deserialize<T>(context.Reader.BaseStream, DefaultOptions);
		}
		#endregion
	}
}