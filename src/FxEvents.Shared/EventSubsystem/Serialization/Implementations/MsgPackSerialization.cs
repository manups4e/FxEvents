using FxEvents.Shared.EventSubsystem.Serialization;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FxEvents.Shared.Serialization.Implementations
{
    public class MsgPackSerialization : ISerialization
    {
        private Log logger = new();

        private bool IsTuple(Type t) => t.Name.StartsWith("Tuple");


        #region Serialization
        public void Serialize(Type type, object value, SerializationContext context)
        {
            if (IsTuple(type))
            {
                logger.Warning("Using Tuple is not advised due to differences between client and server environments and the unavailability of resolvers. Consider using ValueTuple instead.");
                SerializeTuple(type, value, context);
            }
            SerializeObject(type, value, context);
        }

        private void SerializeTuple(Type type, object value, SerializationContext context)
        {
            PropertyInfo[] properties = value.GetType().GetProperties();

            foreach (PropertyInfo property in properties)
            {
                object propertyValue = property.GetValue(value, null);
                Serialize(propertyValue, context);
            }
        }

        private void SerializeObject(Type type, object value, SerializationContext context)
        {
            MessagePackSerializer.Serialize(type, context.Writer.BaseStream, value);
        }

        public void Serialize<T>(T value, SerializationContext context)
        {
            Serialize(typeof(T), value, context);
        }
        #endregion

        #region Deserialization
        public object Deserialize(Type type, SerializationContext context)
        {
            object @return = MessagePackSerializer.Deserialize(type, context.Reader.BaseStream);
            return @return;
        }

        public T Deserialize<T>(SerializationContext context) => Deserialize<T>(typeof(T), context);

        public T Deserialize<T>(Type type, SerializationContext context)
        {
            if (IsTuple(type))
            {
                logger.Warning("Using Tuple is not advised due to differences between client and server environments and the unavailability of resolvers. Consider using ValueTuple instead.");
                return DeserializeTuple<T>(type, context);
            }
            return DeserializeObject<T>(type, context);
        }

        private T DeserializeTuple<T>(Type type, SerializationContext context)
        {

            Type[] generics = type.GetGenericArguments();
            System.Reflection.ConstructorInfo constructor = type.GetConstructor(generics) ??
                                throw new SerializationException(context, type,
                                    $"Could not find suitable constructor for type: {type.Name}");
            List<object> parameters = new List<object>();

            foreach (Type generic in generics)
            {
                object entry = Deserialize(generic, context);
                parameters.Add(entry);
            }

            object tuple = Activator.CreateInstance(type, parameters.ToArray());
            return (T)tuple;
        }

        private T DeserializeObject<T>(Type type, SerializationContext context)
        {
            if(TypeCache<T>.IsSimpleType)
                return (T)TypeConvert.GetNewHolder(context, type);
            else
				return MessagePackSerializer.Deserialize<T>(context.Reader.BaseStream);
        }
        #endregion
    }
}