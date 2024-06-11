using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Serialization.Implementations.MsgPackResolvers;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FxEvents.Shared.Serialization.Implementations
{
    public class MsgPackSerialization : ISerialization
    {
        private delegate T ObjectActivator<out T>();
        private delegate void VoidMethod();
        private Log logger = new();
        private MsgPack.Serialization.SerializationContext _context = new(MsgPack.PackerCompatibilityOptions.None) { SerializationMethod = SerializationMethod.Map, GeneratorOption = SerializationMethodGeneratorOption.Fast };
        public MsgPackSerialization()
        {
            Vector2Resolver vector2 = new(_context);
            Vector3Resolver vector3 = new(_context);
            Vector4Resolver vector4 = new(_context);
            SnowflakeResolver snowflake = new(_context);
            QuaternionResolver quaternion = new(_context);
            KeyValuePairResolver<object, object> kvp = new(_context);
            _context.Serializers.RegisterOverride(vector2);
            _context.Serializers.RegisterOverride(vector3);
            _context.Serializers.RegisterOverride(vector4);
            _context.Serializers.RegisterOverride(quaternion);
            _context.Serializers.RegisterOverride(snowflake);
            _context.Serializers.RegisterOverride(kvp);
        }

        private bool CanCreateInstanceUsingDefaultConstructor(Type t) => t.IsValueType || !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null;
        private static string GetTypeIdentifier(Type type)
        {
            StringBuilder builder = new StringBuilder();
            Type declaring = type;

            builder.Append(type.Namespace);
            builder.Append(".");

            int idx = builder.Length;

            while ((declaring = declaring.DeclaringType) != null)
            {
                builder.Insert(idx, declaring.Name + ".");
            }

            builder.Append(type.Name);

            return builder.ToString();
        }


        #region Serialization
        public void Serialize(Type type, object value, SerializationContext context)
        {
            if (type.Name.StartsWith("Tuple"))
                SerializeTuple(type, value, context);
            else
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
            IMessagePackSingleObjectSerializer ser = MessagePackSerializer.Get(type, _context);
            ser.Pack(context.Writer.BaseStream, value);
        }

        public void Serialize<T>(T value, SerializationContext context)
        {
            Serialize(typeof(T), value, context);
        }
        #endregion

        #region Deserialization
        public object Deserialize(Type type, SerializationContext context)
        {
            IMessagePackSingleObjectSerializer ser = MessagePackSerializer.Get(type, _context);
            object @return = ser.Unpack(context.Reader.BaseStream);
            return @return;
        }

        public T Deserialize<T>(SerializationContext context) => Deserialize<T>(typeof(T), context);

        public T Deserialize<T>(Type type, SerializationContext context)
        {
            if (TypeCache<T>.IsSimpleType)
            {
                object primitive = Deserialize(type, context);
                if (primitive != null) return (T)primitive;
            }

            if (type.Name.StartsWith("Tuple"))
            {
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
            bool canInstance = CanCreateInstanceUsingDefaultConstructor(type);
            if (!canInstance)
            {
                throw new SerializationException(context, type, $"Type {type.Name} is missing its emtpy constructor");
            }
            MessagePackSerializer<T> ser = MessagePackSerializer.Get<T>(_context);
            T @return = ser.Unpack(context.Reader.BaseStream);
            return @return;
        }

        public object DeserializeAnonymously(Type type, SerializationContext context) =>
            Deserialize<object>(type, context);
        #endregion
    }
}