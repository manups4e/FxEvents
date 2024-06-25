using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;

using FxEvents.Shared.EventSubsystem.Serialization;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Message;
using FxEvents.Shared.Models;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FxEvents.Shared.EventSubsystem
{
    // TODO: Concurrency, block a request simliar to a already processed one unless tagged with the [Concurrent] method attribute to combat force spamming events to achieve some kind of bug.
    public delegate Task EventDelayMethod(int ms = 0);
    public delegate Task EventMessagePreparation(string pipeline, int source, IMessage message);
    public delegate void EventMessagePush(string pipeline, int source, string endpoint, Binding binding, byte[] buffer);
    public delegate void EventMessagePushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer);
    public delegate ISource ConstructorCustomActivator<T>(int handle);
    public abstract class BaseGateway
    {
        internal Log Logger = new();
        internal string InboundPipeline;
        internal string OutboundPipeline;
        internal string SignaturePipeline;
        protected abstract ISerialization Serialization { get; }

        private List<Snowflake> eventIds = new List<Snowflake>();
        private List<EventObservable> _queue = new();
        internal EventsDictionary _handlers = new();

        public EventDelayMethod? DelayDelegate { get; set; }
        public EventMessagePreparation? PrepareDelegate { get; set; }
        public EventMessagePush? PushDelegate { get; set; }
        public EventMessagePushLatent? PushDelegateLatent { get; set; }

        public async Task ProcessInboundAsync(int source, string endpoint, Binding binding, byte[] serialized)
        {
#if CLIENT
            bool isServer = false;
#elif SERVER
            bool isServer = true;
#endif
            EventMessage message;
            try
            {
                if (isServer && binding == Binding.Remote)
                    message = serialized.DecryptObject<EventMessage>(source);
                else
                    message = serialized.FromBytes<EventMessage>();
                if (eventIds.Contains(message.Id))
                {
#if CLIENT
                    BaseScript.TriggerServerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
                    Logger.Warning($"Possible tampering detected, the event \"{endpoint}]\" sent by player {API.GetPlayerName(source)} [{source}] has an used ID");
#elif SERVER
                    BaseScript.TriggerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
                    Logger.Warning($"Possible tampering detected, the event \"{endpoint}]\" sent by player {API.GetPlayerName("" + source)} [{source}] has an used ID");
#endif
                }
                else
                {
                    if (eventIds.Count >= 500)
                        eventIds.RemoveAt(0);
                    eventIds.Add(message.Id);
                }
            }
            catch (CryptographicException ex)
            {
                // failed decryption.. possible tampering?
#if CLIENT
                BaseScript.TriggerServerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
                Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}]\" sent by player {API.GetPlayerName(source)} [{source}]");
#elif SERVER
                BaseScript.TriggerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
                Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}]\" sent by player {API.GetPlayerName("" + source)} [{source}]");
#endif
                return;
            }
            await ProcessInvokeAsync(message, source);
        }

        internal async Task ProcessInvokeAsync(EventMessage message, int source)
        {
#if CLIENT
            bool isServer = false;
#elif SERVER
            bool isServer = true;
#endif
            object InvokeDelegate(Delegate @delegate)
            {
                List<object> parameters = new List<object>();
                MethodInfo method = @delegate.Method;
#if CLIENT
                bool takesSource = false;
#elif SERVER
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
                        //MessagePackObject a = context.Deserialize<MessagePackObject>();
                        //if (a.UnderlyingType != type)
                        //{
                        //    if (type.Name.StartsWith("List") || type.Name.StartsWith("Dictionary") || type.Name.StartsWith("Tuple") || a.IsMap || a.IsDictionary || a.IsArray)
                        //    {
                        //        context.Reader.BaseStream.Position = 0;
                        //        var des = context.Deserialize(type);
                        //        holder.Add(des);
                        //    }
                        //    else
                        //    {
                        //        holder.Add(TypeConvert.GetHolder(a, type));
                        //    }
                        //}
                        //else
                        //{
                        //    if (TypeCache.IsSimpleType(type))
                        //        holder.Add(a.ToObject());
                        //    else
                        //    {
                        //        context.Reader.BaseStream.Position = 0;
                        //        var des = context.Deserialize(type);
                        //        holder.Add(des);
                        //    }
                        //}

                        if (TypeCache.IsSimpleType(type))
                        {
                            holder.Add(TypeConvert.GetNewHolder(context, type));
                        }
                        else
                        {
                            holder.Add(context.Deserialize(type));
                        }
                    }
                    else
                    {
                        if (TypeCache.IsSimpleType(type))
                        {
                            if (parameterInfo.DefaultValue != null)
                                holder.Add(parameterInfo.DefaultValue);
                            else
                                holder.Add(default);
                        }
                        else
                        {
                            holder.Add(default);
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
                Tuple<Delegate, Binding> @event = subscription.m_callbacks[0];
                if (!CanExecuteEvent(@event.Item2, message.Sender, isServer))
                    return;

                object result = InvokeDelegate(@event.Item1);

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
                                $"({message.Endpoint} - {subscription.m_callbacks[0].Item1.Method.DeclaringType?.Name ?? "null"}/{subscription.m_callbacks[0].Item1.Method.Name}) The operation was timed out");
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
                PushDelegate(OutboundPipeline, source, message.Endpoint, @event.Item2, data);
                if (EventHub.Debug)
                    Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
            }
            else
            {
                foreach (Tuple<Delegate, Binding> handler in _handlers[message.Endpoint].m_callbacks)
                {
                    if (CanExecuteEvent(handler.Item2, message.Sender, isServer))
                        InvokeDelegate(handler.Item1);
                }
            }
        }

        private bool CanExecuteEvent(Binding handler, EventRemote sender, bool isServer)
        {
            return ((handler == Binding.Remote && sender == EventRemote.Client && isServer) ||
                   (handler == Binding.Remote && sender == EventRemote.Server && !isServer) ||
                   (handler == Binding.Local && sender == EventRemote.Client && !isServer) ||
                   (handler == Binding.Local && sender == EventRemote.Server && !isServer) ||
                   handler == Binding.All) && handler != Binding.None;

        }

        public void ProcessReply(byte[] serialized)
        {
            EventResponseMessage response = serialized.DecryptObject<EventResponseMessage>();
            ProcessReply(response);
        }

        public void ProcessReply(EventResponseMessage response)
        {
            EventObservable waiting = _queue.SingleOrDefault(self => self.Message.Id == response.Id) ?? throw new Exception($"No request matching {response.Id} was found.");

            _queue.Remove(waiting);
            waiting.Callback.Invoke(response.Data);
        }

        internal async Task<EventMessage> CreateAndSendAsync(EventFlowType flow, int source, string endpoint, Binding binding, params object[] args)
        {
#if CLIENT
            bool isServer = false;
#elif SERVER
            bool isServer = true;
#endif
            try
            {
                if (EventHub.Gateway.GetSecret(source).Length == 0)
                {
                    Logger.Info("Client secret not yet available.. waiting for the client to connect");
                    while (EventHub.Gateway.GetSecret(source).Length == 0)
                        await BaseScript.Delay(0);
                }
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

                EventMessage message = new(endpoint, flow, parameters, isServer ? EventRemote.Server : EventRemote.Client);

                if (PrepareDelegate != null)
                {
                    stopwatch.Stop();

                    await PrepareDelegate(InboundPipeline, source, message);
                    stopwatch.Start();
                }

                byte[] data = [];
                if (binding == Binding.Remote || binding == Binding.Local && !isServer)
                {
                    data = message.EncryptObject(source);
                }
                else
                {
                    data = message.ToBytes();
                }

                PushDelegate(InboundPipeline, source, endpoint, binding, data);
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
                EventMessage message = new(endpoint, flow, new List<EventParameter>(), isServer ? EventRemote.Server : EventRemote.Client);
                return message;
            }
        }

        internal async Task<EventMessage> CreateAndSendLatentAsync(EventFlowType flow, int source, string endpoint, int bytePerSecond, params object[] args)
        {
#if CLIENT
            bool isServer = false;
#elif SERVER
            bool isServer = true;
#endif
            if (EventHub.Gateway.GetSecret(source).Length == 0)
            {
                Logger.Info("Client secret not yet available.. waiting for the client to connect");
                while (EventHub.Gateway.GetSecret(source).Length == 0)
                    await BaseScript.Delay(0);
            }
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

            EventMessage message = new(endpoint, flow, parameters, isServer ? EventRemote.Server : EventRemote.Client);

            if (PrepareDelegate != null)
            {
                stopwatch.Stop();

                await PrepareDelegate(InboundPipeline, source, message);
                stopwatch.Start();
            }

            byte[] data = message.EncryptObject(source);

            PushDelegateLatent(InboundPipeline, source, bytePerSecond, message.Endpoint, data);
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

        protected async Task<T> GetInternal<T>(int source, string endpoint, Binding binding, params object[] args)
        {
            StopwatchUtil stopwatch = StopwatchUtil.StartNew();
            EventMessage message = await CreateAndSendAsync(EventFlowType.Circular, source, endpoint, binding, args);
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

        public void Mount(string endpoint, Binding binding, Delegate @delegate)
        {
            if (EventHub.Debug)
                Logger.Debug($"Mounted: {endpoint} - binding {binding}");
            _handlers.Add(endpoint, binding, @delegate);
        }
        public void Unmount(string endpoint)
        {
            _handlers.Remove(endpoint);
        }
    }
}