using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Message;
using FxEvents.Shared.Models;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Serialization;
using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FxEvents.Shared.EventSubsystem
{
    // TODO: Concurrency, block a request simliar to a already processed one unless tagged with the [Concurrent] method attribute to combat force spamming events to achieve some kind of bug.
    public delegate Task EventDelayMethod(int ms = 0);
    public delegate Task EventMessagePreparation(string pipeline, int source, IMessage message);
    public delegate void EventMessagePush(string pipeline, int source, byte[] buffer);
    public delegate ISource ConstructorCustomActivator<T>(int handle);
    public abstract class BaseGateway
    {
        internal Log Logger = new();
        internal string InboundPipeline;
        internal string OutboundPipeline;
        internal string SignaturePipeline;
        protected abstract ISerialization Serialization { get; }

        private List<Tuple<EventMessage, EventHandler>> _processed = new();
        private List<EventObservable> _queue = new();
        private List<EventHandler> _handlers = new();

        public EventDelayMethod? DelayDelegate { get; set; }
        public EventMessagePreparation? PrepareDelegate { get; set; }
        public EventMessagePush? PushDelegate { get; set; }

        public async Task ProcessInboundAsync(int source, byte[] serialized)
        {
            EventMessage message = serialized.DecryptObject<EventMessage>(EventDispatcher.EncryptionKey);
            await ProcessInboundAsync(message, source);
        }

        public async Task ProcessInboundAsync(EventMessage message, int source)
        {
            object InvokeDelegate(EventHandler subscription)
            {
                List<object> parameters = new List<object>();
                bool isServer = API.IsDuplicityVersion();
                Delegate @delegate = subscription.Delegate;
                MethodInfo method = @delegate.Method;
                bool takesSource = method.GetParameters().Any(self => self.GetCustomAttribute<FromSourceAttribute>() != null);
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
                                                                        typeof(string).IsAssignableFrom(self.ParameterType) || typeof(int).IsAssignableFrom(self.ParameterType));
                    Type type = param.ParameterType;
                    if (typeof(ISource).IsAssignableFrom(type))
                    {
                        ConstructorInfo constructor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Any(y => y.ParameterType == typeof(int)));
                        if (constructor == null)
                        {
                            throw new Exception("no constructor to initialize the ISource class");
                        }

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
                        parameters.Add(EventDispatcher.Instance.GetPlayers[source]);
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

                for (int idx = 0; idx < array.Length; idx++)
                {
                    EventParameter parameter = array[idx];
                    Type type = parameterInfos[startingIndex + idx].ParameterType;

                    using SerializationContext context = new SerializationContext(message.Endpoint, $"(Process) Parameter Index {idx}",
                        Serialization, parameter.Data);

                    holder.Add(context.Deserialize(type));
                }

                parameters.AddRange(holder.ToArray());

                return @delegate.DynamicInvoke(parameters.ToArray());
            }

            if (message.Flow == EventFlowType.Circular)
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                EventHandler subscription = _handlers.SingleOrDefault(self => self.Endpoint == message.Endpoint) ??
                                   throw new Exception($"Could not find a handler for endpoint '{message.Endpoint}'");
                object result = InvokeDelegate(subscription);

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
                            $"({message.Endpoint} - {subscription.Delegate.Method.DeclaringType?.Name ?? "null"}/{subscription.Delegate.Method.Name}) The operation was timed out");
                    }
                }

                Type resultType = result?.GetType() ?? typeof(object);
                EventResponseMessage response = new EventResponseMessage(message.Id, message.Endpoint, message.Signature, null);

                if (result != null)
                {
                    using SerializationContext context = new SerializationContext(message.Endpoint, "(Process) Result", Serialization);
                    context.Serialize(resultType, result);
                    response.Data = context.GetData();
                }
                else
                {
                    response.Data = new byte[] { };
                }

                if (PrepareDelegate != null)
                {
                    stopwatch.Stop();

                    await PrepareDelegate(response.Endpoint, source, response);
                    stopwatch.Start();
                }

                byte[] data = response.EncryptObject(EventDispatcher.EncryptionKey);
                PushDelegate(OutboundPipeline, source, data);
                if (EventDispatcher.Debug)
                    Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
            }
            else
            {
                foreach (EventHandler handler in _handlers.Where(self => message.Endpoint == self.Endpoint))
                {
                    InvokeDelegate(handler);
                }
            }
        }

        public void ProcessOutbound(byte[] serialized)
        {
            EventResponseMessage response = serialized.DecryptObject<EventResponseMessage>(EventDispatcher.EncryptionKey);
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
            StopwatchUtil stopwatch = StopwatchUtil.StartNew();
            List<EventParameter> parameters = [];

            for (int idx = 0; idx < args.Length; idx++)
            {
                object argument = args[idx];
                Type type = argument.GetType();

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

            byte[] data = message.EncryptObject(EventDispatcher.EncryptionKey);

            PushDelegate(InboundPipeline, source, data);
            if (EventDispatcher.Debug)
            {
#if CLIENT
                Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName(source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
                Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : API.GetPlayerName("" + source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
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
            if (EventDispatcher.Debug)
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
            if (EventDispatcher.Debug)
                Logger.Debug($"Mounted: {endpoint}");
            _handlers.Add(new EventHandler(endpoint, @delegate));
        }
        public void Unmount(string endpoint)
        {
            if (_handlers.Any(x => x.Endpoint == endpoint))
                _handlers.RemoveAll(x => x.Endpoint == endpoint);
        }
    }
}