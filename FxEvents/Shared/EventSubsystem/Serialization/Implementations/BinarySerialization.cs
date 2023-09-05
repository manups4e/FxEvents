using FxEvents.Shared.Exceptions;
using Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FxEvents.Shared.Serialization.Implementations
{
    public delegate void SerializationObjectActivator(BinaryWriter writer);

    public delegate T DeserializationObjectActivator<out T>(BinaryReader reader);

    [Obsolete("This implementations is obsolete now that MsgPack is available")]
    public class BinarySerialization : ISerialization
    {
        public const string PackMethod = "PackSerializedBytes";
        private Log Logger = new();

        private delegate T ObjectActivator<out T>();

        private delegate void VoidMethod();

        public BinarySerialization()
        {
        }

        public void Serialize(Type type, object? value, SerializationContext context)
        {
            BinaryWriter writer = context.Writer;

            try
            {
                writer.Write(value != null);

                if (value == null)
                {
                    return;
                }

                if (type == typeof(object))
                {
                    throw new SerializationException(context, type, "Cannot serialize values of 'System.Object' type");
                }

                Type nullableUnderlying = Nullable.GetUnderlyingType(type);

                if (nullableUnderlying != null)
                {
                    type = nullableUnderlying;
                }

                if (type.IsEnum)
                {
                    SerializePrimitive(typeof(int), Convert.ChangeType(value, TypeCode.Int32), context);

                    return;
                }

                bool primitive = SerializePrimitive(type, value, context);

                if (primitive) return;

                string typeIdentifier = GetTypeIdentifier(type);

                if (value is IEnumerable enumerable)
                {
                    Type[] generics = type.GetGenericArguments();

                    if (generics.Length == 0)
                    {
                        throw new SerializationException(context, type, "Cannot serialize non-generic IEnumerables");
                    }

                    Type generic = value is IDictionary
                        ? typeof(KeyValuePair<,>)
                            .MakeGenericType(generics[0], generics[1])
                        : generics[0];

                    int count = value switch
                    {
                        Array array => array.Length,
                        IDictionary dictionary => dictionary.Count,
                        IList list => list.Count,
                        ICollection collection => collection.Count,
                        _ => throw new SerializationException(context, type,
                            $"Enumerable type {type.Name} is not supported. Try adding [Serialization] and the partial keyword, or manually implement packing/unpacking logic.")
                    };

                    writer.Write(count);

                    foreach (object entry in enumerable)
                    {
                        Serialize(generic, entry, context);
                    }
                }
                else if (typeIdentifier.StartsWith("System.Tuple`"))
                {
                    Type[] generics = type.GetGenericArguments();
                    MethodInfo method = GetType().GetMethod("Serialize",
                        new[] { typeof(Type), typeof(object), typeof(SerializationContext) });
                    ParameterExpression instanceParam = Expression.Parameter(typeof(BinarySerialization), "instance");
                    ParameterExpression typeParam = Expression.Parameter(typeof(Type), "type");
                    ParameterExpression valueParam = Expression.Parameter(type, "value");
                    ParameterExpression contextParam = Expression.Parameter(typeof(SerializationContext), "context");

                    for (int idx = 0; idx < generics.Length; idx++)
                    {
                        Type generic = generics[idx];
                        MethodCallExpression call = Expression.Call(instanceParam, method!, typeParam,
                            Expression.Convert(Expression.Property(valueParam, $"Item{idx + 1}"), typeof(object)),
                            contextParam);
                        Action action = (Action)Expression.Lambda(typeof(Action), Expression.Block(new[]
                        {
                            instanceParam,
                            typeParam,
                            contextParam,
                            valueParam
                        },
                            Expression.Assign(instanceParam, Expression.Constant(this, typeof(BinarySerialization))),
                            Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext))),
                            Expression.Assign(typeParam, Expression.Constant(generic, typeof(Type))),
                            Expression.Assign(valueParam, Expression.Constant(value, type)),
                            call
                        )).Compile();

                        action.Invoke();
                    }
                }
                else if (typeIdentifier == "System.Collections.Generic.KeyValuePair`2")
                {
                    Type[] generics = type.GetGenericArguments();
                    MethodInfo method = GetType().GetMethod("Serialize",
                        new[] { typeof(Type), typeof(object), typeof(SerializationContext) });
                    ParameterExpression instanceParam = Expression.Parameter(typeof(BinarySerialization), "instance");
                    ParameterExpression typeParam = Expression.Parameter(typeof(Type), "type");
                    ParameterExpression contextParam = Expression.Parameter(typeof(SerializationContext), "context");
                    ParameterExpression pairParam = Expression.Parameter(type, "pair");
                    ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");
                    MethodCallExpression call = Expression.Call(instanceParam, method!, typeParam, valueParam, contextParam);

                    void CallSerialization(Type genericType, string property)
                    {
                        Action action = (Action)Expression.Lambda(typeof(Action), Expression.Block(new[]
                            {
                                instanceParam,
                                typeParam,
                                contextParam,
                                pairParam,
                                valueParam
                            },
                            Expression.Assign(instanceParam, Expression.Constant(this, typeof(BinarySerialization))),
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
                else
                    switch (value)
                    {
                        case DateTime date:
                            writer.Write(date.Ticks);

                            break;
                        case TimeSpan span:
                            writer.Write(span.Ticks);

                            break;
                        default:
                            {
                                MethodInfo method = type.GetMethod(PackMethod, BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.HasThis, new[] { typeof(BinaryWriter) }, null);
                                if (method is null)
                                    throw new SerializationException(context, type, $"Failed to find \"{PackMethod}\" method; are you sure you have annotated the type with [Serialization] and the partial keyword?");

                                ParameterExpression parameter = Expression.Parameter(typeof(BinaryWriter), "writer");
                                MethodCallExpression expression = Expression.Call(Expression.Constant(value, type), method, parameter);
                                SerializationObjectActivator activator = (SerializationObjectActivator)Expression.Lambda(
                                    typeof(SerializationObjectActivator), expression,
                                    parameter).Compile();

                                activator.Invoke(context.Writer!);

                                break;
                            }
                    }
            }
            catch (SerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SerializationException(context, type,
                    $"Failed to find \"{PackMethod}\" method; are you sure you have annotated the type {type.Name} with [Serialization] and the partial keyword?", ex);
            }
        }

        public object DeserializeAnonymously(Type type, SerializationContext context) =>
            Deserialize<object>(type, context);

        public T Deserialize<T>(Type type, SerializationContext context)
        {
            try
            {
                bool exists = context.Reader!.ReadBoolean();

                if (!exists)
                {
                    return default;
                }
                Type nullableUnderlying = Nullable.GetUnderlyingType(type);

                if (nullableUnderlying != null)
                {
                    type = nullableUnderlying;
                }
                if (type.IsEnum)
                {
                    object handle = DeserializePrimitive(typeof(int), context);
                    UnaryExpression expression = Expression.Convert(Expression.Constant(handle, typeof(int)), type);
                    Delegate conversion = Expression.Lambda(expression).Compile();
                    object result = conversion.DynamicInvoke();

                    return (T)result;
                }
                object primitive = DeserializePrimitive(type, context);

                if (primitive != null) return (T)primitive;

                if (typeof(IEnumerable).IsAssignableFrom(type))
                {
                    Type[] generics = type.GetGenericArguments();
                    Type generic = typeof(IDictionary).IsAssignableFrom(type)
                        ? typeof(KeyValuePair<,>)
                            .MakeGenericType(generics[0], generics[1])
                        : generics[0];

                    int count = context.Reader.ReadInt32();
                    ParameterExpression countParam = Expression.Parameter(typeof(int), "count");
                    Func<object> lambda = (Func<object>)Expression.Lambda(
                        Expression.Block(new[] { countParam },
                            Expression.Assign(countParam, Expression.Constant(count, typeof(int))),
                            Expression.NewArrayBounds(generic, countParam)
                        )).Compile();
                    Type arrayType = generic.MakeArrayType();
                    object array = lambda.Invoke();
                    ConstantExpression pointer = Expression.Constant(array, arrayType);
                    MethodInfo method = GetType()
                        .GetMethod("DeserializeAnonymously", new[] { typeof(Type), typeof(SerializationContext) });
                    ParameterExpression instanceParam = Expression.Parameter(typeof(BinarySerialization), "instance");
                    ParameterExpression genericParam = Expression.Parameter(typeof(Type), "generic");
                    ParameterExpression contextParam = Expression.Parameter(typeof(SerializationContext), "context");

                    BlockExpression GetBlock(params Expression[] expressions)
                    {
                        return Expression.Block(new[]
                        {
                            instanceParam, genericParam, contextParam
                        }, new[]
                        {
                            Expression.Assign(instanceParam, Expression.Constant(this, typeof(BinarySerialization))),
                            Expression.Assign(genericParam, Expression.Constant(generic, typeof(Type))),
                            Expression.Assign(contextParam, Expression.Constant(context, typeof(SerializationContext)))
                        }.Concat(expressions));
                    }

                    for (int idx = 0; idx < count; idx++)
                    {
                        MethodCallExpression call = Expression.Call(instanceParam, method!, genericParam, contextParam);
                        BinaryExpression idxAssign =
                            Expression.Assign(Expression.ArrayAccess(pointer, Expression.Constant(idx, typeof(int))),
                                Expression.Convert(call, generic));

                        ((VoidMethod)Expression.Lambda(typeof(VoidMethod), GetBlock(idxAssign)).Compile()).Invoke();
                    }

                    if (arrayType == typeof(T))
                    {
                        return (T)array;
                    }

                    ObjectActivator<T> activator =
                        (ObjectActivator<T>)Expression.Lambda(typeof(ObjectActivator<T>), Expression.New(type))
                            .Compile();
                    T enumerable = activator.Invoke();

                    switch (enumerable)
                    {
                        case IDictionary _:
                            {
                                foreach (object pair in (Array)array)
                                {
                                    ConstantExpression instance = Expression.Constant(enumerable, type);
                                    ConstantExpression pairParam = Expression.Constant(pair, generic);
                                    ParameterExpression keyParam = Expression.Parameter(generics[0], "key");
                                    ParameterExpression valueParam = Expression.Parameter(generics[1], "value");
                                    MethodCallExpression call = Expression.Call(instance, type.GetMethod("Add",
                                        new[] { generics[0], generics[1] })!,
                                        keyParam,
                                        valueParam);

                                    BlockExpression block = Expression.Block(new[]
                                        {
                                        keyParam,
                                        valueParam
                                    },
                                        Expression.Assign(keyParam, Expression.Property(pairParam, "Key")),
                                        Expression.Assign(valueParam, Expression.Property(pairParam, "Value")),
                                        call
                                    );

                                    Action action = (Action)Expression.Lambda(typeof(Action), block).Compile();

                                    action.Invoke();
                                }

                                break;
                            }
                        case IList list:
                            {
                                foreach (object entry in (Array)array)
                                {
                                    list.Add(entry);
                                }

                                break;
                            }
                    }

                    return enumerable;
                }

                string typeIdentifier = GetTypeIdentifier(type);
                if (typeIdentifier.StartsWith("System.Tuple`"))
                {
                    Type[] generics = type.GetGenericArguments();
                    ConstructorInfo constructor = type.GetConstructor(generics) ??
                                      throw new SerializationException(context, type,
                                          $"Could not find suitable constructor for type: {type.Name}");
                    List<Expression> parameters = new List<Expression>();

                    foreach (Type generic in generics)
                    {
                        object entry = Deserialize(generic, context);

                        parameters.Add(Expression.Constant(entry, generic));
                    }

                    NewExpression expression = Expression.New(constructor, parameters);

                    if (typeof(T) == typeof(object))
                    {
                        Type generic = typeof(ObjectActivator<>).MakeGenericType(type);
                        Delegate activator = Expression.Lambda(generic, expression).Compile();

                        return (T)activator.DynamicInvoke();
                    }
                    else
                    {
                        ObjectActivator<T> activator =
                            (ObjectActivator<T>)Expression.Lambda(typeof(ObjectActivator<T>), expression).Compile();

                        return activator.Invoke();
                    }
                }

                if (typeIdentifier == "System.Collections.Generic.KeyValuePair`2")
                {
                    Type[] generics = type.GetGenericArguments();
                    ConstructorInfo constructor = type.GetConstructor(generics) ??
                                      throw new SerializationException(context, type,
                                          $"Could not find suitable constructor for type: {type.Name}");

                    object key = DeserializeAnonymously(generics[0], context);
                    object value = DeserializeAnonymously(generics[1], context);
                    ParameterExpression keyParam = Expression.Parameter(generics[0], "key");
                    ParameterExpression valueParam = Expression.Parameter(generics[1], "value");
                    BlockExpression block = Expression.Block(
                        new[] { keyParam, valueParam },
                        Expression.Assign(keyParam, Expression.Constant(key, generics[0])),
                        Expression.Assign(valueParam, Expression.Constant(value, generics[1])),
                        Expression.New(constructor, keyParam, valueParam)
                    );

                    if (typeof(T) == typeof(object))
                    {
                        Type generic = typeof(ObjectActivator<>).MakeGenericType(type);
                        Delegate activator = Expression.Lambda(generic, block).Compile();

                        return (T)activator.DynamicInvoke();
                    }
                    else
                    {
                        ObjectActivator<T> activator =
                            (ObjectActivator<T>)Expression.Lambda(typeof(ObjectActivator<T>), block).Compile();

                        return activator.Invoke();
                    }
                }

                if (type == typeof(DateTime))
                {
                    return (T)(object)new DateTime(context.Reader.ReadInt64());
                }

                if (type == typeof(TimeSpan))
                {
                    return (T)(object)new TimeSpan(context.Reader.ReadInt64());
                }

                {
                    ConstructorInfo constructor = type.GetConstructors().FirstOrDefault(self =>
                        self.GetParameters().FirstOrDefault()?.ParameterType == typeof(BinaryReader));

                    if (constructor == null)
                    {
                        throw new SerializationException(context, type,
                            $"Could not find suitable constructor for type: {type.Name}");
                    }

                    ParameterExpression parameter = Expression.Parameter(typeof(BinaryReader), "reader");
                    NewExpression expression = Expression.New(constructor, parameter);
                    if (typeof(T) == typeof(object))
                    {
                        Type generic = typeof(DeserializationObjectActivator<>).MakeGenericType(type);
                        Delegate activator = Expression.Lambda(generic, expression, parameter).Compile();

                        return (T)activator.DynamicInvoke(context.Reader);
                    }
                    else
                    {

                        DeserializationObjectActivator<T> activator = (DeserializationObjectActivator<T>)Expression
                            .Lambda(typeof(DeserializationObjectActivator<T>), expression, parameter).Compile();

                        return activator.Invoke(context.Reader);
                    }
                }
            }
            catch (SerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SerializationException(context, type, $"Failed deserialization of type '{type.Name}'", ex);
            }
        }

        public bool SerializePrimitive(Type type, object value, SerializationContext context)
        {
            try
            {
                if (context.Writer == null) throw new Exception("SerializationContext.Writer is null.");
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        context.Writer.Write((bool)value);

                        return true;
                    case TypeCode.Byte:
                        context.Writer.Write((byte)value);

                        return true;
                    case TypeCode.Char:
                        context.Writer.Write((char)value);

                        return true;
                    case TypeCode.Double:
                        context.Writer.Write((double)value);

                        return true;
                    case TypeCode.Int16:
                        context.Writer.Write((short)value);

                        return true;
                    case TypeCode.Int32:
                        context.Writer.Write((int)value);

                        return true;
                    case TypeCode.Int64:
                        context.Writer.Write((long)value);

                        return true;
                    case TypeCode.Single:
                        context.Writer.Write((float)value);

                        return true;
                    case TypeCode.String:
                        context.Writer.Write((string)value);

                        return true;
                    case TypeCode.SByte:
                        context.Writer.Write((sbyte)value);

                        return true;
                    case TypeCode.UInt16:
                        context.Writer.Write((ushort)value);

                        return true;
                    case TypeCode.UInt32:
                        context.Writer.Write((uint)value);

                        return true;
                    case TypeCode.UInt64:
                        context.Writer.Write((ulong)value);

                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                throw new SerializationException(context, type, $"Could not serialize primitive: {type}", ex);
            }
        }

        public object? DeserializePrimitive(Type type, SerializationContext context)
        {
            try
            {
                if (context.Reader == null) throw new Exception("SerializationContext.Reader is null.");
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        return context.Reader.ReadBoolean();
                    case TypeCode.Byte:
                        return context.Reader.ReadByte();
                    case TypeCode.Char:
                        return context.Reader.ReadChar();
                    case TypeCode.Double:
                        return context.Reader.ReadDouble();
                    case TypeCode.Int16:
                        return context.Reader.ReadInt16();
                    case TypeCode.Int32:
                        return context.Reader.ReadInt32();
                    case TypeCode.Int64:
                        return context.Reader.ReadInt64();
                    case TypeCode.Single:
                        return context.Reader.ReadSingle();
                    case TypeCode.String:
                        return context.Reader.ReadString();
                    case TypeCode.SByte:
                        return context.Reader.ReadSByte();
                    case TypeCode.UInt16:
                        return context.Reader.ReadUInt16();
                    case TypeCode.UInt32:
                        return context.Reader.ReadUInt32();
                    case TypeCode.UInt64:
                        return context.Reader.ReadUInt64();
                }

                return default;
            }
            catch (Exception ex)
            {
                throw new SerializationException(context, type, $"Could not deserialize primitive: {type}", ex);
            }
        }

        public void Serialize<T>(T value, SerializationContext context) => Serialize(typeof(T), value, context);
        public object Deserialize(Type type, SerializationContext context) => Deserialize<object>(type, context);
        public T Deserialize<T>(SerializationContext context) => Deserialize<T>(typeof(T), context);

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
    }
}