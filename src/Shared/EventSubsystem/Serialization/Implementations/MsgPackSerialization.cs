using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Serialization.Implementations.MsgPackResolvers;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
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
        private MsgPack.Serialization.SerializationContext _context = new(MsgPack.PackerCompatibilityOptions.None) { SerializationMethod = SerializationMethod.Map };
        public MsgPackSerialization()
        {
            Vector2Resolver vector2 = new(_context);
            Vector3Resolver vector3 = new(_context);
            Vector4Resolver vector4 = new(_context);
            SnowflakeResolver snowflake = new(_context);
            QuaternionResolver quaternion = new(_context);
            KeyValuePairResolver<object, object> kvp = new(_context);
            TupleResolver<object> tuple1 = new(_context);
            TupleResolver<object, object> tuple2 = new(_context);
            TupleResolver<object, object, object> tuple3 = new(_context);
            TupleResolver<object, object, object, object> tuple4 = new(_context);
            TupleResolver<object, object, object, object, object> tuple5 = new(_context);
            TupleResolver<object, object, object, object, object, object> tuple6 = new(_context);
            TupleResolver<object, object, object, object, object, object, object> tuple7 = new(_context);
            _context.Serializers.RegisterOverride(vector2);
            _context.Serializers.RegisterOverride(vector3);
            _context.Serializers.RegisterOverride(vector4);
            _context.Serializers.RegisterOverride(quaternion);
            _context.Serializers.RegisterOverride(snowflake);
            _context.Serializers.RegisterOverride(kvp);
            _context.Serializers.RegisterOverride(tuple1);
            _context.Serializers.RegisterOverride(tuple2);
            _context.Serializers.RegisterOverride(tuple3);
            _context.Serializers.RegisterOverride(tuple4);
            _context.Serializers.RegisterOverride(tuple5);
            _context.Serializers.RegisterOverride(tuple6);
            _context.Serializers.RegisterOverride(tuple7);
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
            string typeIdentifier = GetTypeIdentifier(type);
            if (EventDispatcher.Debug)
            {
                logger.Debug("SERIALIZE - typeIdentifier: " + type.FullName);
            }
            if (type.Name.StartsWith("Tuple"))
                SerializeTuple(type, value, context);
            else
                SerializeObject(type, value, context);
        }
        private void SerializeKeyValuePair(Type type, object value, SerializationContext context)
        {
            Type[] generics = type.GetGenericArguments();
            MethodInfo method = GetType().GetMethod("Serialize", new[] { typeof(Type), typeof(object), typeof(SerializationContext) });

            ParameterExpression instanceParam = Expression.Parameter(typeof(MsgPackSerialization), "instance");
            ParameterExpression typeParam = Expression.Parameter(typeof(Type), "type");
            ParameterExpression contextParam = Expression.Parameter(typeof(SerializationContext), "context");
            ParameterExpression pairParam = Expression.Parameter(type, "pair");
            ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");

            MethodCallExpression call = Expression.Call(instanceParam, method, typeParam, valueParam, contextParam);

            void CallSerialization(Type genericType, string property)
            {
                Action action = Expression.Lambda<Action>(
                    Expression.Block(
                        new[] { instanceParam, typeParam, contextParam, pairParam, valueParam },
                        Expression.Assign(instanceParam, Expression.Constant(this, typeof(MsgPackSerialization))),
                        Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext))),
                        Expression.Assign(typeParam, Expression.Constant(genericType, typeof(Type))),
                        Expression.Assign(pairParam, Expression.Constant(value, type)),
                        Expression.Assign(valueParam, Expression.Convert(Expression.Property(pairParam, property), typeof(object))),
                        call
                    )
                ).Compile();

                action.Invoke();
            }

            CallSerialization(generics[0], "Key");
            CallSerialization(generics[1], "Value");
        }

        private void SerializeTuple(Type type, object value, SerializationContext context)
        {
            Type[] generics = type.GetGenericArguments();
            MethodInfo method = GetType().GetMethod("Serialize", new[] { typeof(Type), typeof(object), typeof(SerializationContext) });

            ParameterExpression instanceParam = Expression.Parameter(typeof(MsgPackSerialization), "instance");
            ParameterExpression typeParam = Expression.Parameter(typeof(Type), "type");
            ParameterExpression valueParam = Expression.Parameter(type, "value");
            ParameterExpression contextParam = Expression.Parameter(typeof(SerializationContext), "context");

            for (int idx = 0; idx < generics.Length; idx++)
            {
                Type generic = generics[idx];
                MethodCallExpression call = Expression.Call(instanceParam, method, typeParam,
                    Expression.Convert(Expression.Property(valueParam, $"Item{idx + 1}"), typeof(object)),
                    contextParam);

                Action action = Expression.Lambda<Action>(
                    Expression.Block(
                        new[] { instanceParam, typeParam, contextParam, valueParam },
                        Expression.Assign(instanceParam, Expression.Constant(this, typeof(MsgPackSerialization))),
                        Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext))),
                        Expression.Assign(typeParam, Expression.Constant(generic, typeof(Type))),
                        Expression.Assign(valueParam, Expression.Constant(value, type)),
                        call
                    )
                ).Compile();

                action.Invoke();
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
            string typeIdentifier = GetTypeIdentifier(type);
            if (EventDispatcher.Debug)
            {
                logger.Debug("DESERIALIZE - typeIdentifier: " + typeIdentifier);
            }

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

        private T DeserializeKeyValuePair<T>(Type type, SerializationContext context)
        {
            Type[] generics = type.GetGenericArguments();
            System.Reflection.ConstructorInfo constructor = type.GetConstructor(generics) ?? throw new SerializationException(context, type, $"Could not find suitable constructor for type: {type.Name}");


            object key = DeserializeAnonymously(generics[0], context);
            object value = DeserializeAnonymously(generics[1], context);

            object kpv = Activator.CreateInstance(type, key, value);

            return (T)kpv;
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