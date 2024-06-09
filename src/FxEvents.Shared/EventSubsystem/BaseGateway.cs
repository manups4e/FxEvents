using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Message;
using FxEvents.Shared.Models;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FxEvents.Shared.EventSubsystem
{
    // TODO: Concurrency, block a request simliar to a already processed one unless tagged with the [Concurrent] method attribute to combat force spamming events to achieve some kind of bug.
    public delegate Task EventDelayMethod(int ms = 0);
    public delegate Task EventMessagePreparation(string pipeline, int source, IMessage message);
    public delegate void EventMessagePush(string pipeline, int source, byte[] buffer);
    public delegate void EventMessagePushLatent(string pipeline, int source, int bytePerSecond, byte[] buffer);
    public delegate ISource ConstructorCustomActivator<T>(int handle);
    public abstract class BaseGateway
    {
        internal Log Logger = new();
        internal string InboundPipeline;
        internal string OutboundPipeline;
        internal string SignaturePipeline;
        protected abstract ISerialization Serialization { get; }

        private List<EventObservable> _queue = new();
        internal EventsDictionary _handlers = new();

        public EventDelayMethod? DelayDelegate { get; set; }
        public EventMessagePreparation? PrepareDelegate { get; set; }
        public EventMessagePush? PushDelegate { get; set; }
        public EventMessagePushLatent? PushDelegateLatent { get; set; }

        public async Task ProcessInboundAsync(int source, byte[] serialized)
        {
            EventMessage message = serialized.DecryptObject<EventMessage>(source);
            await ProcessInboundAsync(message, source);
        }

        public async Task ProcessInboundAsync(EventMessage message, int source)
        {
            object InvokeDelegate(Delegate @delegate)
            {
                List<object> parameters = new List<object>();
                MethodInfo method = @delegate.Method;
#if CLIENT
                bool isServer = false;
                bool takesSource = false;
#elif SERVER
                bool isServer = true;
                bool takesSource = method.GetParameters().Any(self => self.GetCustomAttribute<FromSourceAttribute>() != null);
#endif
                int startingIndex = takesSource && isServer ? 1 : 0;

                object CallInternalDelegate()
                {
                    return @delegate.DynamicInvoke(parameters.ToArray());
                }

                if (isServer && takesSource)
                {
                    if (method.GetParameters().Where(self => self.GetCustomAttribute<FromSourceAttribute>() != null).Count() > 1)
                        throw new Exception($"{message.Endpoint} cannot have more than 1 \"FromSource\" attribute applied to its parameters.");
                    if (method.GetParameters().ToList().IndexOf(method.GetParameters().FirstOrDefault(self => self.GetCustomAttribute<FromSourceAttribute>() != null)) != 0)
                        throw new Exception($"{message.Endpoint} \"FromSource\" attribute can ONLY be applied to first parameter.");

                    ParameterInfo param = method.GetParameters().FirstOrDefault(self => typeof(ISource).IsAssignableFrom(self.ParameterType) ||
                                                                        typeof(Player).IsAssignableFrom(self.ParameterType) ||
                                                                        typeof(string).IsAssignableFrom(self.ParameterType) || 
                                                                        typeof(int).IsAssignableFrom(self.ParameterType));
                    Type type = param.ParameterType;
                    if (typeof(ISource).IsAssignableFrom(type))
                    {
                        ConstructorInfo constructor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Any(y => y.ParameterType == typeof(int))) 
                            ?? throw new Exception("no constructor to initialize the ISource class");

                        ParameterExpression parameter = Expression.Parameter(typeof(int), "handle");
                        NewExpression expression = Expression.New(constructor, parameter);
                        if (typeof(ISource) == typeof(object))
                        {
                            Type generic = typeof(ConstructorCustomActivator<>).MakeGenericType(type);
                            Delegate activator = Expression.Lambda(generic, expression, parameter).Compile();

                            ISource objectInstance = (ISource)activator.DynamicInvoke(source);
                            parameters.Add(objectInstance);
                        }
                        else
                        {
                            ConstructorCustomActivator<ISource> activator = (ConstructorCustomActivator<ISource>)Expression
                                .Lambda(typeof(ConstructorCustomActivator<ISource>), expression, parameter).Compile();

                            ISource objectInstance = activator.Invoke(source);
                            parameters.Add(objectInstance);
                        }
                    }
                    else if (typeof(Player).IsAssignableFrom(type))
                    {
                        parameters.Add(EventHub.Instance.GetPlayers[source]);
                    }
                    else if (typeof(string).IsAssignableFrom(type))
                    {
                        parameters.Add(source.ToString());
                    }
                    else if (typeof(int).IsAssignableFrom(type))
                    {
                        parameters.Add(source);
                    }
                }

                if (message.Parameters == null)
                {
                    return CallInternalDelegate();
                }

                EventParameter[] array = message.Parameters.ToArray();
                List<object> holder = new List<object>();
                ParameterInfo[] parameterInfos = @delegate.Method.GetParameters();

                for (int idx = startingIndex; idx < parameterInfos.Length; idx++)
                {
                    ParameterInfo parameterInfo = parameterInfos[idx];
                    Type type = parameterInfo.ParameterType;
                    if (idx - startingIndex < array.Length)
                    {
                        EventParameter parameter = array[idx - startingIndex];
                        using SerializationContext context = new(message.Endpoint, $"(Process) Parameter Index {idx - startingIndex}", Serialization, parameter.Data);
                        MessagePackObject a = context.Deserialize<MessagePackObject>();
                        if (a.UnderlyingType != type)
                        {
                            object obj = a.ToObject();

                            TypeCode typeCode = Type.GetTypeCode(type);

                            switch (typeCode)
                            {
                                case TypeCode.String:
                                    holder.Add(obj as string ?? throw new Exception($"Cannot convert {a.UnderlyingType} to String"));
                                    break;
                                case TypeCode.Object:
                                    try
                                    {
                                        holder.Add(Activator.CreateInstance(type, obj));
                                    }
                                    catch (Exception e)
                                    {
                                        throw new Exception($"FxEvents - Cannot create instance of type {type.Name} with parameter {obj.ToJson()}, maybe a missing constructor?", e);
                                    }
                                    break;
                                case TypeCode.DBNull:
                                case TypeCode.DateTime:
                                    holder.Add(obj);
                                    break;
                                case TypeCode.Byte:
                                case TypeCode.SByte:
                                case TypeCode.Int16:
                                case TypeCode.Int32:
                                case TypeCode.Int64:
                                case TypeCode.UInt16:
                                case TypeCode.UInt32:
                                case TypeCode.UInt64:
                                    if (obj is IConvertible convertible)
                                    {
                                        try
                                        {
                                            holder.Add(Convert.ChangeType(convertible, type));
                                        }
                                        catch (InvalidCastException)
                                        {
                                            throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                        }
                                    }
                                    else
                                        holder.Add(GetDefaultForType(type));
                                    break;
                                case TypeCode.Boolean:
                                    bool booleanValue;
                                    if (bool.TryParse(obj.ToString(), out booleanValue))
                                        holder.Add(booleanValue);
                                    else
                                        throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                    break;
                                case TypeCode.Char:
                                    char charValue;
                                    if (char.TryParse(obj.ToString(), out charValue))
                                        holder.Add(charValue);
                                    else
                                        throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                    break;
                                case TypeCode.Decimal:
                                    decimal decimalValue;
                                    if (decimal.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue))
                                        holder.Add(decimalValue);
                                    else
                                        throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                    break;
                                case TypeCode.Single:
                                    float floatValue;
                                    if (float.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out floatValue))
                                        holder.Add(floatValue);
                                    else
                                        throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                    break;
                                case TypeCode.Double:
                                    double doubleValue;
                                    if (double.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                                        holder.Add(doubleValue);
                                    else
                                        throw new Exception($"Cannot convert {a.UnderlyingType} to {type.Name}");
                                    break;
                                default:
                                    holder.Add(GetDefaultForType(type));
                                    break;
                            }
                        }
                        else
                        {
                            if (TypeCache.IsSimpleType(type))
                                holder.Add(a.ToObject());
                            else
                                holder.Add(Activator.CreateInstance(type, a.ToObject()));
                        }
                    }
                    else
                    {
                        if (TypeCache.IsSimpleType(type))
                        {
                            if(parameterInfo.DefaultValue != null)
                                holder.Add(parameterInfo.DefaultValue);
                            else
                                holder.Add(default);
                        }
                        else
                        {
                            object a = Activator.CreateInstance(type);
                            holder.Add(a);
                        }
                    }
                }

                parameters.AddRange(holder.ToArray());

                try
                {
                    return @delegate.DynamicInvoke(parameters.ToArray());
                }
                catch (Exception ex)
                {
                    if (isServer && takesSource)
                        parameters.RemoveAt(0);
                    Logger.Error($"Handler [{message.Endpoint}] with parameters [{parameters.ToJson()}] threw an error.\n" + ex);
                    return null;
                }
            }

            if (message.Flow == EventFlowType.Circular)
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                int hasSingle = _handlers[message.Endpoint].m_callbacks.Count;
                if (hasSingle != 1)
                {
                    if (hasSingle > 1)
                        throw new EventException($"Found multiple callback handlers for event {message.Endpoint}, only 1 allowed.");
                    else if (hasSingle == 0)
                        throw new EventException($"Callback handler for event {message.Endpoint} not found.");
                }

                EventEntry subscription = _handlers[message.Endpoint];
                object result = InvokeDelegate(subscription.m_callbacks[0]);

                if (result.GetType().IsGenericType)
                {
                    if (result.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        using CancellationTokenSource token = new CancellationTokenSource();

                        Task task = (Task)result;
                        Task timeout = DelayDelegate!(10000);
                        Task completed = await Task.WhenAny(task, timeout);

                        if (completed == task)
                        {
                            token.Cancel();

#if CLIENT
                            await task;
#elif SERVER
                            await task.ConfigureAwait(false);
#endif

                            result = ((dynamic)task).Result;
                        }
                        else
                        {
                            throw new EventTimeoutException(
                                $"({message.Endpoint} - {subscription.m_callbacks[0].Method.DeclaringType?.Name ?? "null"}/{subscription.m_callbacks[0].Method.Name}) The operation was timed out");
                        }
                    }
                }

                Type resultType = result?.GetType() ?? typeof(object);
                EventResponseMessage response = new EventResponseMessage(message.Id, message.Endpoint, null);

                if (result != null)
                {
                    using SerializationContext context = new SerializationContext(message.Endpoint, "(Process) Result", Serialization);
                    context.Serialize(resultType, result);
                    response.Data = context.GetData();
                }
                else
                {
                    response.Data = [];
                }

                byte[] data = response.EncryptObject(source);
                PushDelegate(OutboundPipeline, source, data);
                if (EventHub.Debug)
                    Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
            }
            else
            {
                foreach (Delegate handler in _handlers[message.Endpoint].m_callbacks)
                {
                    InvokeDelegate(handler);
                }
            }
        }

        private static object GetDefaultForType(Type type)
        {
            // Determine the default value for the given type
            if (type == typeof(string)) return string.Empty;
            if (type == typeof(byte)) return 0;
            if (type == typeof(sbyte)) return 0;
            if (type == typeof(bool)) return false;
            if (type == typeof(char)) return '\0';
            if (type == typeof(DateTime)) return DateTime.MinValue;
            if (type == typeof(decimal)) return 0M;
            if (type == typeof(float)) return 0F;
            if (type == typeof(double)) return 0D;
            if (type == typeof(short)) return 0;
            if (type == typeof(int)) return 0;
            if (type == typeof(long)) return 0L;
            if (type == typeof(ushort)) return 0U;
            if (type == typeof(uint)) return 0U;
            if (type == typeof(ulong)) return 0UL;
            return null; // Fallback to null for unsupported types
        }

        public void ProcessOutbound(byte[] serialized)
        {
            EventResponseMessage response = serialized.DecryptObject<EventResponseMessage>();
            ProcessOutbound(response);
        }

        public void ProcessOutbound(EventResponseMessage response)
        {
            EventObservable waiting = _queue.SingleOrDefault(self => self.Message.Id == response.Id) ?? throw new Exception($"No request matching {response.Id} was found.");

            _queue.Remove(waiting);
            waiting.Callback.Invoke(response.Data);
        }

        protected async Task<EventMessage> SendInternal(EventFlowType flow, int source, string endpoint, params object[] args)
        {
            try
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                List<EventParameter> parameters = [];

                for (int idx = 0; idx < args.Length; idx++)
                {
                    object argument = args[idx];
                    Type type = argument.GetType();
                    //Debug.WriteLine($"outbound {endpoint} - index: {idx}, type:{type.FullName}, value:{argument.ToJson()}");

                    using SerializationContext context = new(endpoint, $"(Send) Parameter Index '{idx}'", Serialization);

                    context.Serialize(type, argument);
                    parameters.Add(new EventParameter(context.GetData()));
                }

                EventMessage message = new(endpoint, flow, parameters);

                if (PrepareDelegate != null)
                {
                    stopwatch.Stop();

                    await PrepareDelegate(InboundPipeline, source, message);
                    stopwatch.Start();
                }

                byte[] data = message.EncryptObject(source);

                PushDelegate(InboundPipeline, source, data);
                if (EventHub.Debug)
                {
#if CLIENT
                    Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName(source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
                    Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName("" + source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#endif
                }
                return message;
            }
            catch (Exception ex)
            {
                Logger.Error($"{endpoint} - {ex.ToString()}");
                EventMessage message = new(endpoint, flow, new List<EventParameter>());
                return message;
            }
        }

        protected async Task<EventMessage> SendInternalLatent(EventFlowType flow, int source, string endpoint, int bytePerSecond, params object[] args)
        {
            StopwatchUtil stopwatch = StopwatchUtil.StartNew();
            List<EventParameter> parameters = [];

            for (int idx = 0; idx < args.Length; idx++)
            {
                object argument = args[idx];
                Type type = argument.GetType();
                //Debug.WriteLine($"outbound latent {endpoint} - index: {idx}, type:{type.FullName}, value:{argument.ToJson()}");

                using SerializationContext context = new(endpoint, $"(Send) Parameter Index '{idx}'", Serialization);

                context.Serialize(type, argument);
                parameters.Add(new EventParameter(context.GetData()));
            }

            EventMessage message = new(endpoint, flow, parameters);

            if (PrepareDelegate != null)
            {
                stopwatch.Stop();

                await PrepareDelegate(InboundPipeline, source, message);
                stopwatch.Start();
            }

            byte[] data = message.EncryptObject(source);

            PushDelegateLatent(InboundPipeline, source, bytePerSecond, data);
            if (EventHub.Debug)
            {
#if CLIENT
                Logger.Debug($"[{endpoint} {flow}] Sent latent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName(source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
                Logger.Debug($"[{endpoint} {flow}] Sent latent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName("" + source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#endif
            }
            return message;
        }

        protected async Task<T> GetInternal<T>(int source, string endpoint, params object[] args)
        {
            StopwatchUtil stopwatch = StopwatchUtil.StartNew();
            EventMessage message = await SendInternal(EventFlowType.Circular, source, endpoint, args);
            EventValueHolder<T> holder = new EventValueHolder<T>();
            TaskCompletionSource<bool> TokenLoading = new();

            _queue.Add(new EventObservable(message, data =>
            {
                using SerializationContext context = new SerializationContext(endpoint, "(Get) Response", Serialization, data);

                holder.Data = data;
                holder.Value = context.Deserialize<T>();

                TokenLoading.SetResult(true);
            }));

            await TokenLoading.Task;

            double elapsed = stopwatch.Elapsed.TotalMilliseconds;
            if (EventHub.Debug)
            {
#if CLIENT
                Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : API.GetPlayerName(source))} of {holder.Data.Length} byte(s) in {elapsed}ms");
#elif SERVER
                Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : API.GetPlayerName("" + source))} of {holder.Data.Length} byte(s) in {elapsed}ms");
#endif
            }
            return holder.Value;
        }

        public void Mount(string endpoint, Delegate @delegate)
        {
            if (EventHub.Debug)
                Logger.Debug($"Mounted: {endpoint}");
            _handlers.Add(endpoint, @delegate);
        }
        public void Unmount(string endpoint)
        {
            _handlers.Remove(endpoint);
        }
    }
}