using FxEvents.Shared.Exceptions;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        public void Serialize(Type type, object value, SerializationContext context)
        {
            var typeIdentifier = GetTypeIdentifier(type);
            if (typeIdentifier == "System.Collections.Generic.KeyValuePair`2")
            {
                var generics = type.GetGenericArguments();
                var method = GetType().GetMethod("Serialize",
                    new[] { typeof(Type), typeof(object), typeof(SerializationContext) });
                var instanceParam = Expression.Parameter(typeof(MsgPackSerialization), "instance");
                var typeParam = Expression.Parameter(typeof(Type), "type");
                var contextParam = Expression.Parameter(typeof(SerializationContext), "context");
                var pairParam = Expression.Parameter(type, "pair");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var call = Expression.Call(instanceParam, method!, typeParam, valueParam, contextParam);

                void CallSerialization(Type genericType, string property)
                {
                    var action = (Action)Expression.Lambda(typeof(Action), Expression.Block(new[]
                        {
                                instanceParam,
                                typeParam,
                                contextParam,
                                pairParam,
                                valueParam
                            },
                        Expression.Assign(instanceParam, Expression.Constant(this, typeof(MsgPackSerialization))),
                        Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext))),
                        Expression.Assign(typeParam, Expression.Constant(genericType, typeof(Type))),
                        Expression.Assign(pairParam, Expression.Constant(value, type)),
                        Expression.Assign(valueParam,
                            Expression.Convert(Expression.Property(pairParam, property), typeof(object))),
                        call
                    )).Compile();

                    action.Invoke();
                }

                CallSerialization(generics[0], "Key");
                CallSerialization(generics[1], "Value");
            }
            else if (typeIdentifier.StartsWith("System.Tuple`"))
            {
                var generics = type.GetGenericArguments();
                var method = GetType().GetMethod("Serialize",
                    new[] { typeof(Type), typeof(object), typeof(SerializationContext) });
                var instanceParam = Expression.Parameter(typeof(MsgPackSerialization), "instance");
                var typeParam = Expression.Parameter(typeof(Type), "type");
                var valueParam = Expression.Parameter(type, "value");
                var contextParam = Expression.Parameter(typeof(SerializationContext), "context");

                for (var idx = 0; idx < generics.Length; idx++)
                {
                    var generic = generics[idx];
                    var call = Expression.Call(instanceParam, method!, typeParam,
                        Expression.Convert(Expression.Property(valueParam, $"Item{idx + 1}"), typeof(object)),
                        contextParam);
                    var action = (Action)Expression.Lambda(typeof(Action), Expression.Block(new[]
                    {
                        instanceParam,
                        typeParam,
                        contextParam,
                        valueParam
                    },
                    Expression.Assign(instanceParam, Expression.Constant(this, typeof(MsgPackSerialization))),
                    Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext))),
                    Expression.Assign(typeParam, Expression.Constant(generic, typeof(Type))),
                    Expression.Assign(valueParam, Expression.Constant(value, type)),
                    call
                    )).Compile();

                    action.Invoke();
                }
            }
            else
            {
                var ser = MessagePackSerializer.Get(type, _context);
                ser.Pack(context.Writer.BaseStream, value);
            }
        }

        public void Serialize<T>(T value, SerializationContext context)
        {
            Serialize(typeof(T), value, context);
        }

        public object Deserialize(Type type, SerializationContext context)
        {
            var ser = MessagePackSerializer.Get(type, _context);
            var @return = ser.Unpack(context.Reader.BaseStream);
            return @return;
        }

        public T Deserialize<T>(SerializationContext context) => Deserialize<T>(typeof(T), context);

        public T Deserialize<T>(Type type, SerializationContext context)
        {
            var canInstance = CanCreateInstanceUsingDefaultConstructor(type);
            var typeIdentifier = GetTypeIdentifier(type);

            if (TypeCache<T>.IsSimpleType)
            {
                var primitive = Deserialize(type, context);
                if (primitive != null) return (T)primitive;
            }
            if (typeIdentifier == "System.Collections.Generic.KeyValuePair`2")
            {
                var generics = type.GetGenericArguments();
                var constructor = type.GetConstructor(generics) ??
                                  throw new SerializationException(context, type,
                                      $"Could not find suitable constructor for type: {type.Name}");

                var key = DeserializeAnonymously(generics[0], context);
                var value = DeserializeAnonymously(generics[1], context);
                var keyParam = Expression.Parameter(generics[0], "key");
                var valueParam = Expression.Parameter(generics[1], "value");
                var block = Expression.Block(
                    new[] { keyParam, valueParam },
                    Expression.Assign(keyParam, Expression.Constant(key, generics[0])),
                    Expression.Assign(valueParam, Expression.Constant(value, generics[1])),
                    Expression.New(constructor, keyParam, valueParam)
                );

                if (typeof(T) == typeof(object))
                {
                    var generic = typeof(ObjectActivator<>).MakeGenericType(type);
                    var activator = Expression.Lambda(generic, block).Compile();

                    return (T)activator.DynamicInvoke();
                }
                else
                {
                    var activator =
                        (ObjectActivator<T>)Expression.Lambda(typeof(ObjectActivator<T>), block).Compile();

                    return activator.Invoke();
                }
            }
            else if (typeIdentifier.StartsWith("System.Tuple`"))
            {
                var generics = type.GetGenericArguments();
                var constructor = type.GetConstructor(generics) ??
                                  throw new SerializationException(context, type,
                                      $"Could not find suitable constructor for type: {type.Name}");
                var parameters = new List<Expression>();

                foreach (var generic in generics)
                {
                    var entry = Deserialize(generic, context);

                    parameters.Add(Expression.Constant(entry, generic));
                }

                var expression = Expression.New(constructor, parameters);

                if (typeof(T) == typeof(object))
                {
                    var generic = typeof(ObjectActivator<>).MakeGenericType(type);
                    var activator = Expression.Lambda(generic, expression).Compile();

                    return (T)activator.DynamicInvoke();
                }
                else
                {
                    var activator =
                        (ObjectActivator<T>)Expression.Lambda(typeof(ObjectActivator<T>), expression).Compile();

                    return activator.Invoke();
                }
            }
            else
            {
                if (!canInstance)
                {
                    throw new SerializationException(context, type, $"Type {type.Name} is missing its emtpy constructor");
                }
                var ser = MessagePackSerializer.Get<T>(_context);
                var @return = ser.Unpack(context.Reader.BaseStream);
                return @return;
            }
        }

        public object DeserializeAnonymously(Type type, SerializationContext context) =>
            Deserialize<object>(type, context);

        private static string GetTypeIdentifier(Type type)
        {
            var builder = new StringBuilder();
            var declaring = type;

            builder.Append(type.Namespace);
            builder.Append(".");

            var idx = builder.Length;

            while ((declaring = declaring.DeclaringType) != null)
            {
                builder.Insert(idx, declaring.Name + ".");
            }

            builder.Append(type.Name);

            return builder.ToString();
        }
    }
}