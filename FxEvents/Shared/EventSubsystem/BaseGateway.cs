using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Message;
using FxEvents.Shared.Models;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Serialization;
using CitizenFX.Core.Native;
using CitizenFX.Core;

namespace FxEvents.Shared.EventSubsystem
{
    // TODO: Concurrency, block a request simliar to a already processed one unless tagged with the [Concurrent] method attribute to combat force spamming events to achieve some kind of bug.
    public delegate Task EventDelayMethod(int ms = 0);
    public delegate Task EventMessagePreparation(string pipeline, ISource source, IMessage message);
    public delegate void EventMessagePush(string pipeline, ISource source, byte[] buffer);

    public abstract class BaseGateway
    {
        internal Log Logger = new();
        protected abstract ISerialization Serialization { get; }

        private List<Tuple<EventMessage, EventHandler>> _processed = new();
        private List<EventObservable> _queue = new();
        private List<EventHandler> _handlers = new();

        public EventDelayMethod? DelayDelegate { get; set; }
        public EventMessagePreparation? PrepareDelegate { get; set; }
        public EventMessagePush? PushDelegate { get; set; }

        public async Task ProcessInboundAsync(ISource source, byte[] serialized)
        {
            using var context = new SerializationContext(EventConstant.InboundPipeline, "(Process) In", Serialization, serialized);
            var message = context.Deserialize<EventMessage>();

            await ProcessInboundAsync(message, source);
        }

        public async Task ProcessInboundAsync(EventMessage message, ISource source)
        {
            object InvokeDelegate(EventHandler subscription)
            {
                var parameters = new List<object>();
                var @delegate = subscription.Delegate;
                var method = @delegate.Method;
                var takesSource = method.GetParameters().Any(self => self.ParameterType == source.GetType());
                var startingIndex = takesSource ? 1 : 0;

                object CallInternalDelegate()
                {
                    return @delegate.DynamicInvoke(parameters.ToArray());
                }

                if (takesSource)
                {
                    parameters.Add(source);
                }

                if (message.Parameters == null) return CallInternalDelegate();

                var array = message.Parameters.ToArray();
                var holder = new List<object>();
                var parameterInfos = @delegate.Method.GetParameters();

                for (var idx = 0; idx < array.Length; idx++)
                {
                    var parameter = array[idx];
                    var type = parameterInfos[startingIndex + idx].ParameterType;

                    using var context = new SerializationContext(message.Endpoint, $"(Process) Parameter Index {idx}",
                        Serialization, parameter.Data);

                    holder.Add(context.Deserialize(type));
                }

                parameters.AddRange(holder.ToArray());

                return @delegate.DynamicInvoke(parameters.ToArray());
            }

            if (message.Flow == EventFlowType.Circular)
            {
                var stopwatch = StopwatchUtil.StartNew();
                var subscription = _handlers.SingleOrDefault(self => self.Endpoint == message.Endpoint) ??
                                   throw new Exception($"Could not find a handler for endpoint '{message.Endpoint}'");
                var result = InvokeDelegate(subscription);

                if (result.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                {
                    using var token = new CancellationTokenSource();

                    var task = (Task)result;
                    var timeout = DelayDelegate!(10000);
                    var completed = await Task.WhenAny(task, timeout);

                    if (completed == task)
                    {
                        token.Cancel();

                        await task.ConfigureAwait(false);

                        result = (object)((dynamic)task).Result;
                    }
                    else
                    {
                        throw new EventTimeoutException(
                            $"({message.Endpoint} - {subscription.Delegate.Method.DeclaringType?.Name ?? "null"}/{subscription.Delegate.Method.Name}) The operation was timed out");
                    }
                }

                var resultType = result?.GetType() ?? typeof(object);
                var response = new EventResponseMessage(message.Id, message.Endpoint, message.Signature, null);

                if (result != null)
                {
                    using var context = new SerializationContext(message.Endpoint, "(Process) Result", Serialization);
                    context.Serialize(resultType, result);
                    response.Data = context.GetData();
                }
                else
                {
                    response.Data = Array.Empty<byte>();
                }

                using (var context = new SerializationContext(message.Endpoint, "(Process) Response", Serialization))
                {
                    context.Serialize(response);

                    var data = context.GetData();

                    PushDelegate(EventConstant.OutboundPipeline, source, data);
                    if (EventDispatcher.Debug)
                        Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
                }
            }
            else
            {
                foreach (var handler in _handlers.Where(self => message.Endpoint == self.Endpoint))
                {
                    InvokeDelegate(handler);
                }
            }
        }

        public void ProcessOutbound(byte[] serialized)
        {
            using var context = new SerializationContext(EventConstant.OutboundPipeline, "(Process) Out", Serialization, serialized);
            var response = context.Deserialize<EventResponseMessage>();

            ProcessOutbound(response);
        }

        public void ProcessOutbound(EventResponseMessage response)
        {
            var waiting = _queue.SingleOrDefault(self => self.Message.Id == response.Id) ?? throw new Exception($"No request matching {response.Id} was found.");

            _queue.Remove(waiting);
            waiting.Callback.Invoke(response.Data);
        }

        protected async Task<EventMessage> SendInternal(EventFlowType flow, ISource source, string endpoint, params object[] args)
        {
            var stopwatch = StopwatchUtil.StartNew();
            var parameters = new List<EventParameter>();

            for (var idx = 0; idx < args.Length; idx++)
            {
                var argument = args[idx];
                var type = argument.GetType();

                using var context = new SerializationContext(endpoint, $"(Send) Parameter Index '{idx}'", Serialization);

                context.Serialize(type, argument);
                parameters.Add(new EventParameter(context.GetData()));
            }

            var message = new EventMessage(endpoint, flow, parameters);

            if (PrepareDelegate != null)
            {
                stopwatch.Stop();

                await PrepareDelegate(EventConstant.InboundPipeline, source, message);

                stopwatch.Start();
            }

            using (var context = new SerializationContext(endpoint, "(Send) Output", Serialization))
            {
                context.Serialize(message);

                var data = context.GetData();

                PushDelegate(EventConstant.InboundPipeline, source, data);
                if (EventDispatcher.Debug) 
                { 

#if CLIENT
                    Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source.Handle == -1?"Server":API.GetPlayerName(source.Handle))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
                    Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source.Handle == -1?"Server":API.GetPlayerName(""+source.Handle))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#endif
                }

                    return message;
            }
        }

        protected async Task<T> GetInternal<T>(ISource source, string endpoint, params object[] args)
        {
            var stopwatch = StopwatchUtil.StartNew();
            var message = await SendInternal(EventFlowType.Circular, source, endpoint, args);
            var token = new CancellationTokenSource();
            var holder = new EventValueHolder<T>();

            _queue.Add(new EventObservable(message, data =>
            {
                using var context = new SerializationContext(endpoint, "(Get) Response", Serialization, data);

                holder.Data = data;
                holder.Value = context.Deserialize<T>();

                token.Cancel();
            }));

            while (!token.IsCancellationRequested)
            {
                await DelayDelegate();
            }


            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            if (EventDispatcher.Debug)
            {
#if CLIENT
                Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source.Handle == -1 ? "Server" : API.GetPlayerName(source.Handle))} of {holder.Data.Length} byte(s) in {elapsed}ms");
#elif SERVER
                Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source.Handle == -1?"Server":API.GetPlayerName(""+source.Handle))} of {holder.Data.Length} byte(s) in {elapsed}ms");
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