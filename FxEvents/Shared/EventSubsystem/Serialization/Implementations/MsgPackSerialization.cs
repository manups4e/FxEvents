using FxEvents.Shared.Exceptions;
using FxEvents.Shared.TypeExtensions;
using Logger;
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
                logger.Debug("SERIALIZE - typeIdentifier: " + typeIdentifier);
            }

            if (typeIdentifier.StartsWith("System.Collections.Generic.KeyValuePair`2"))
                SerializeKeyValuePair(type, value, context);
            else if (typeIdentifier.StartsWith("System.Tuple`"))
                SerializeTuple(type, value, context);
            else if (typeIdentifier.StartsWith("CitizenFX.Core.Vector"))
                SerializeVector(type, value, context);
            else if (typeIdentifier == "CitizenFX.Core.Quaternion")
                SerializeQuaternion(value, context);
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

        private void SerializeVector(Type type, object value, SerializationContext context)
        {
            IMessagePackSingleObjectSerializer ser = MessagePackSerializer.Get(typeof(float[]), context);
            float[] vectorData;

            if (type == typeof(Vector2))
                vectorData = ((Vector2)value).ToArray();
            else if (type == typeof(Vector3))
                vectorData = ((Vector3)value).ToArray();
            else
                vectorData = ((Vector4)value).ToArray();

            ser.Pack(context.Writer.BaseStream, vectorData);
        }

        private void SerializeQuaternion(object value, SerializationContext context)
        {
            IMessagePackSingleObjectSerializer ser = MessagePackSerializer.Get(typeof(float[]), context);
            float[] quaternionData = ((CitizenFX.Core.Quaternion)value).ToArray();
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

            if (typeIdentifier.StartsWith("System.Collections.Generic.KeyValuePair`2"))
                return DeserializeKeyValuePair<T>(type, context);
            else if (typeIdentifier.StartsWith("System.Tuple`"))
                return DeserializeTuple<T>(type, context);
            else if (typeIdentifier == "CitizenFX.Core.Vector2" || typeIdentifier == "CitizenFX.Core.Vector3" || typeIdentifier == "CitizenFX.Core.Vector4")
                return DeserializeVector<T>(type, context);
            else if (typeIdentifier == "CitizenFX.Core.Quaternion")
                return DeserializeQuaternion<T>(context);
            else
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

        private T DeserializeVector<T>(Type type, SerializationContext context)
        {
            MessagePackSerializer<float[]> ser = MessagePackSerializer.Get<float[]>(_context);
            float[] @return = ser.Unpack(context.Reader.BaseStream);
            object vec;
            if (type == typeof(Vector2))
                vec = new Vector2(@return);
            else if (type == typeof(Vector3))
                vec = new Vector3(@return);
            else
                vec = new Vector4(@return);
            return (T)vec;
        }

        private T DeserializeQuaternion<T>(SerializationContext context)
        {
            MessagePackSerializer<float[]> ser = MessagePackSerializer.Get<float[]>(_context);
            float[] @return = ser.Unpack(context.Reader.BaseStream);
            object quat = new Quaternion(@return);
            return (T)quat;
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