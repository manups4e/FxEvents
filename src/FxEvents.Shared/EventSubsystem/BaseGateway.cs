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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FxEvents.Shared.EventSubsystem
{
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
		internal bool takesSource = false;
#if CLIENT
		internal bool isServer = false;
#elif SERVER
        internal bool isServer = true;
#endif
		protected abstract ISerialization Serialization { get; }
		private readonly HashSet<Snowflake> _eventIdSet = new();
		private readonly Queue<Snowflake> _eventIdQueue = new();
		private readonly object _eventIdLock = new();

		private List<EventObservable> _queue = new();
		internal EventsDictionary _handlers = new();

		public EventDelayMethod? DelayDelegate { get; set; }
		public EventMessagePreparation? PrepareDelegate { get; set; }
		public EventMessagePush? PushDelegate { get; set; }
		public EventMessagePushLatent? PushDelegateLatent { get; set; }
		public int DefaultTimeoutMs { get; set; } = 10000;

		public async Task ProcessInboundAsync(int source, string endpoint, Binding binding, byte[] serialized)
		{
			EventMessage message;
			try
			{
				if (isServer && binding == Binding.Local)
					message = serialized.FromBytes<EventMessage>();
				else
					message = serialized.DecryptObject<EventMessage>(source);

				bool isDuplicate;
				lock (_eventIdLock)
				{
					isDuplicate = _eventIdSet.Contains(message.Id);
					if (!isDuplicate)
					{
						if (_eventIdQueue.Count >= 500)
						{
							var oldest = _eventIdQueue.Dequeue();
							_eventIdSet.Remove(oldest);
						}
						_eventIdQueue.Enqueue(message.Id);
						_eventIdSet.Add(message.Id);
					}
				}

				if (isDuplicate)
				{
#if CLIENT
					BaseScript.TriggerServerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
					Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {API.GetPlayerName(source)} [{source}] has a used ID");
#elif SERVER
                    BaseScript.TriggerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
                    Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {API.GetPlayerName("" + source)} [{source}] has a used ID");
#endif
					return; 
				}
			}
			catch (CryptographicException)
			{
#if CLIENT
				BaseScript.TriggerServerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
				Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}\" sent by player {API.GetPlayerName(source)} [{source}]");
#elif SERVER
                BaseScript.TriggerEvent("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
                Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}\" sent by player {API.GetPlayerName("" + source)} [{source}]");
#endif
				return;
			}

			await ProcessInvokeAsync(message, source);
		}

		internal async Task ProcessInvokeAsync(EventMessage message, int source)
		{
			object InvokeDelegate(Delegate @delegate)
			{
				List<object> parameters = new List<object>();
				MethodInfo method = @delegate.Method;
#if SERVER
                bool takesSource = method.GetParameters().Any(self => self.GetCustomAttribute<FromSourceAttribute>() != null);
#endif
				int startingIndex = takesSource && isServer ? 1 : 0;

				object CallInternalDelegate()
				{
					return @delegate.DynamicInvoke(parameters.ToArray());
				}

				if (isServer && takesSource)
				{
					ParameterInfo param = method.GetParameters().FirstOrDefault(self =>
						typeof(ISource).IsAssignableFrom(self.ParameterType) ||
						typeof(Player).IsAssignableFrom(self.ParameterType) ||
						typeof(string).IsAssignableFrom(self.ParameterType) ||
						typeof(int).IsAssignableFrom(self.ParameterType));

					Type type = param.ParameterType;
					if (typeof(ISource).IsAssignableFrom(type))
					{
						ConstructorInfo constructor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Any(y => y.ParameterType == typeof(int)))
							?? throw new Exception("No constructor found to initialize the ISource class.");

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
				ParameterInfo[] parameterInfos = method.GetParameters();

				for (int idx = startingIndex; idx < parameterInfos.Length; idx++)
				{
					ParameterInfo parameterInfo = parameterInfos[idx];
					Type type = parameterInfo.ParameterType;

					if (idx - startingIndex < array.Length)
					{
						EventParameter parameter = array[idx - startingIndex];
						using SerializationContext context = new(message.Endpoint, $"(Process) Parameter Index {idx - startingIndex}", Serialization, parameter.Data);

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
				EventEntry subscription = _handlers[message.Endpoint];

				int hasSingle = subscription.m_callbacks.Count;
				if (hasSingle != 1)
				{
					if (hasSingle > 1)
						throw new EventException($"Found multiple callback handlers for event {message.Endpoint}, only 1 allowed.");
					else if (hasSingle == 0)
						throw new EventException($"Callback handler for event {message.Endpoint} not found.");
				}

				Tuple<Delegate, Binding> @event = subscription.m_callbacks[0];
				if (!CanExecuteEvent(@event.Item2, message.Sender))
					return;

				object result = InvokeDelegate(@event.Item1);

				if (result?.GetType().IsGenericType == true && result.GetType().GetGenericTypeDefinition() == typeof(Task<>))
				{
					using CancellationTokenSource token = new CancellationTokenSource();

					Task task = (Task)result;

					// check for the new attribute or go to default 10000ms;
					EventTimeoutAttribute timeoutAttr = @event.Item1.Method.GetCustomAttribute<EventTimeoutAttribute>();
					int timeoutMs = timeoutAttr?.TimeoutMs ?? DefaultTimeoutMs;

					Task timeout = DelayDelegate!(timeoutMs);
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
							$"({message.Endpoint} - {@event.Item1.Method.DeclaringType?.Name ?? "null"}/{@event.Item1.Method.Name}) Operation timed out after {timeoutMs}ms.");
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
					if (CanExecuteEvent(handler.Item2, message.Sender))
						InvokeDelegate(handler.Item1);
				}
			}
		}

		private bool CanExecuteEvent(Binding handler, EventRemote sender)
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
			EventObservable waiting = _queue.SingleOrDefault(self => self.Message.Id == response.Id)
				?? throw new Exception($"No request matching {response.Id} was found for event {response.Endpoint}.");

			_queue.Remove(waiting);
			waiting.Callback.Invoke(response.Data);
		}

		internal async Task<EventMessage> CreateAndSendAsync(EventFlowType flow, int source, string endpoint, Binding binding, params object[] args)
		{
			try
			{
				StopwatchUtil stopwatch = StopwatchUtil.StartNew();
				List<EventParameter> parameters = new();

				for (int idx = 0; idx < args.Length; idx++)
				{
					object argument = args[idx];
					Type type = argument.GetType();

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

				if (EventHub.Gateway.GetSecret(source).Length == 0) return null;

				byte[] data;
				if (binding == Binding.Remote || (binding == Binding.Local && !isServer))
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
				Logger.Error($"{endpoint} - {ex}");
				return new EventMessage(endpoint, flow, new List<EventParameter>(), isServer ? EventRemote.Server : EventRemote.Client);
			}
		}

		internal async Task<EventMessage> CreateAndSendLatentAsync(EventFlowType flow, int source, string endpoint, int bytePerSecond, params object[] args)
		{
			StopwatchUtil stopwatch = StopwatchUtil.StartNew();
			List<EventParameter> parameters = new();

			for (int idx = 0; idx < args.Length; idx++)
			{
				object argument = args[idx];
				Type type = argument.GetType();

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

			if (EventHub.Gateway.GetSecret(source).Length == 0) return null;

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
			MethodInfo method = @delegate.Method;
			var fromSourceParams = method.GetParameters().Where(self => self.GetCustomAttribute<FromSourceAttribute>() != null).ToList();

			if (fromSourceParams.Count > 1)
				throw new EventException($"[Mount Error] Handler '{endpoint}' cannot have more than one 'FromSource' attribute.");

			if (fromSourceParams.Count == 1 && method.GetParameters().ToList().IndexOf(fromSourceParams[0]) != 0)
				throw new EventException($"[Mount Error] Handler '{endpoint}' 'FromSource' attribute MUST be placed on the first parameter.");

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