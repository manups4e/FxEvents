using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.EventSubsystem.Serialization;
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.Message;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.TypeExtensions;
using Logger;
using System;
using System.Collections.Concurrent;
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

		private readonly ConcurrentDictionary<Snowflake, Action<byte[]>> _pendingResponses = new();
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

		private static readonly ConcurrentDictionary<Type, Func<Task, object>> _taskResultGetterCache = new();

		private static object GetTaskResult(Task task)
		{
			var getter = _taskResultGetterCache.GetOrAdd(task.GetType(), t =>
			{
				ParameterExpression parameter = Expression.Parameter(typeof(Task), "t");
				UnaryExpression castTask = Expression.Convert(parameter, t);
				MemberExpression property = Expression.Property(castTask, "Result");
				UnaryExpression castResult = Expression.Convert(property, typeof(object));

				return Expression.Lambda<Func<Task, object>>(castResult, parameter).Compile();
			});

			return getter(task);
		}

		internal async Task ProcessInvokeAsync(EventMessage message, int source)
		{
			object InvokeDelegate(EventCallback callbackReg)
			{
				Delegate @delegate = callbackReg.Callback;
				int startingIndex = (callbackReg.TakesSource && isServer) ? 1 : 0;
				int totalParams = startingIndex + callbackReg.Parameters.Length;
				object[] parameters = new object[totalParams];

				if (isServer && callbackReg.TakesSource)
				{
					switch (callbackReg.SourceKind)
					{
						case SourceParamKind.Player:
							parameters[0] = EventHub.Instance.GetPlayers[source];
							break;
						case SourceParamKind.String:
							parameters[0] = source.ToString();
							break;
						case SourceParamKind.Int:
							parameters[0] = source;
							break;
						case SourceParamKind.ISource:
							parameters[0] = callbackReg.SourceActivator!(source);
							break;
					}
				}

				if (message.Parameters != null)
				{
					EventParameter[] array = message.Parameters as EventParameter[] ?? message.Parameters.ToArray();

					for (int i = 0; i < callbackReg.Parameters.Length; i++)
					{
						var pMeta = callbackReg.Parameters[i];
						if (i < array.Length)
						{
							EventParameter parameter = array[i];
							using SerializationContext context = new(message.Endpoint, $"(Process) Parameter Index {i}", Serialization, parameter.Data);

							if (pMeta.IsSimpleType)
							{
								parameters[startingIndex + i] = TypeConvert.GetNewHolder(context, pMeta.ParameterType);
							}
							else
							{
								parameters[startingIndex + i] = context.Deserialize(pMeta.ParameterType);
							}
						}
						else
						{
							parameters[startingIndex + i] = pMeta.DefaultValue;
						}
					}
				}
				else
				{
					for (int i = 0; i < callbackReg.Parameters.Length; i++)
					{
						parameters[startingIndex + i] = callbackReg.Parameters[i].DefaultValue;
					}
				}

				try
				{
					return @delegate.DynamicInvoke(parameters);
				}
				catch (Exception ex)
				{
					Logger.Error($"Handler [{message.Endpoint}] threw an error.\n" + ex);
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

				EventCallback @event = subscription.m_callbacks[0];
				if (!CanExecuteEvent(@event.Binding, message.Sender))
					return;

				object result = InvokeDelegate(@event);

				if (result?.GetType().IsGenericType == true && result.GetType().GetGenericTypeDefinition() == typeof(Task<>))
				{
					using CancellationTokenSource token = new CancellationTokenSource();

					Task task = (Task)result;

					int timeoutMs = @event.CustomTimeoutMs ?? DefaultTimeoutMs;

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
						// Estrazione sicura del risultato senza Reflection diretta
						result = GetTaskResult(task);
					}
					else
					{
						throw new EventTimeoutException(
							$"({message.Endpoint} - {@event.Callback.Method.DeclaringType?.Name ?? "null"}/{@event.Callback.Method.Name}) Operation timed out after {timeoutMs}ms.");
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
				PushDelegate(OutboundPipeline, source, message.Endpoint, @event.Binding, data);
				if (EventHub.Debug)
					Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
			}
			else
			{
				foreach (EventCallback handler in _handlers[message.Endpoint].m_callbacks)
				{
					if (CanExecuteEvent(handler.Binding, message.Sender))
						InvokeDelegate(handler);
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
			if (_pendingResponses.TryRemove(response.Id, out Action<byte[]> callback))
			{
				callback(response.Data);
			}
			else if (EventHub.Debug)
			{
				Logger.Warning($"No request matching {response.Id} was found for event {response.Endpoint}.");
			}
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
			if (message == null) return default;

			TaskCompletionSource<byte[]> tcs = new();
			_pendingResponses[message.Id] = data => tcs.TrySetResult(data);

			Task timeoutTask = DelayDelegate != null ? DelayDelegate(DefaultTimeoutMs) : Task.Delay(DefaultTimeoutMs);
			Task completed = await Task.WhenAny(tcs.Task, timeoutTask);

			if (completed != tcs.Task)
			{
				_pendingResponses.TryRemove(message.Id, out _);
				throw new EventTimeoutException($"({endpoint}) Operation timed out after {DefaultTimeoutMs}ms waiting for response.");
			}

			byte[] responseData = await tcs.Task;
			using SerializationContext context = new(endpoint, "(Get) Response", Serialization, responseData);
			T value = context.Deserialize<T>();

			double elapsed = stopwatch.Elapsed.TotalMilliseconds;
			if (EventHub.Debug)
			{
#if CLIENT
				Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : API.GetPlayerName(source))} of {responseData?.Length ?? 0} byte(s) in {elapsed}ms");
#elif SERVER
                Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : API.GetPlayerName("" + source))} of {responseData?.Length ?? 0} byte(s) in {elapsed}ms");
#endif
			}
			return value;
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