global using CitizenFX.FiveM.Shared;
#if CLIENT
using CitizenFX.FiveM.Client.Entities;
#elif SERVER
using CitizenFX.FiveM.Server.Entities;
#endif
using CitizenFX.FiveM.Shared.Serialization;
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
		internal bool takesSource = false;

#if CLIENT
		internal bool isServer = false;
#elif SERVER
		internal bool isServer = true;
#endif

		protected abstract ISerialization Serialization { get; }

		// HashSet per ricerca O(1) anziché O(N) sui messaggi duplicati
		private readonly HashSet<Snowflake> _processedEventIds = [];
		private readonly Queue<Snowflake> _eventIdsOrder = new();
		private readonly List<EventObservable> _queue = [];
		internal EventsDictionary _handlers = new();

		public EventDelayMethod? DelayDelegate { get; set; }
		public EventMessagePreparation? PrepareDelegate { get; set; }
		public EventMessagePush? PushDelegate { get; set; }
		public EventMessagePushLatent? PushDelegateLatent { get; set; }

		public async Task ProcessInboundAsync(int source, string endpoint, Binding binding, byte[] serialized)
		{
			EventMessage message;
			try
			{
				message = (isServer && binding == Binding.Local)
					? serialized.FromBytes<EventMessage>()
					: serialized.DecryptObject<EventMessage>(source);

				lock (_processedEventIds)
				{
					if (!_processedEventIds.Add(message.Id))
					{
#if CLIENT
						API.EmitServer("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
						Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {GetPlayerName(source)} [{source}] has an used ID");
#elif SERVER
						API.EmitLocal("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
						Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {GetPlayerName("" + source)} [{source}] has an used ID");
#endif
					}
					else
					{
						_eventIdsOrder.Enqueue(message.Id);
						if (_eventIdsOrder.Count > 500)
						{
							_processedEventIds.Remove(_eventIdsOrder.Dequeue());
						}
					}
				}
			}
			catch (CryptographicException)
			{
#if CLIENT
				API.EmitServer("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
				Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}\" sent by player {GetPlayerName(source)} [{source}]");
#elif SERVER
				API.EmitLocal("fxevents:tamperingprotection", source, endpoint, TamperType.EDITED_ENCRYPTED_DATA);
				Logger.Warning($"Possible tampering detected, impossible to decrypt event message \"{endpoint}\" sent by player {GetPlayerName("" + source)} [{source}]");
#endif
				return;
			}

			await ProcessInvokeAsync(message, source);
		}

		internal async Task ProcessInvokeAsync(EventMessage message, int source)
		{
			object? InvokeDelegate(Delegate @delegate)
			{
				List<object?> parameters = [];
				MethodInfo method = @delegate.Method;
				ParameterInfo[] parameterInfos = method.GetParameters();

#if SERVER
				bool hasSourceAttribute = parameterInfos.Any(p => p.GetCustomAttribute<FromSourceAttribute>() != null);
#else
				bool hasSourceAttribute = false;
#endif
				int startingIndex = hasSourceAttribute && isServer ? 1 : 0;

				if (isServer && hasSourceAttribute)
				{
					if (parameterInfos.Count(p => p.GetCustomAttribute<FromSourceAttribute>() != null) > 1)
						throw new InvalidOperationException($"{message.Endpoint} cannot have more than 1 \"FromSource\" attribute applied to its parameters.");

					if (Array.FindIndex(parameterInfos, p => p.GetCustomAttribute<FromSourceAttribute>() != null) != 0)
						throw new InvalidOperationException($"{message.Endpoint} \"FromSource\" attribute can ONLY be applied to first parameter.");

					ParameterInfo sourceParam = parameterInfos[0];
					Type pType = sourceParam.ParameterType;

					if (typeof(ISource).IsAssignableFrom(pType))
					{
						ConstructorInfo constructor = pType.GetConstructors().FirstOrDefault(x => x.GetParameters().Any(y => y.ParameterType == typeof(int)))
							?? throw new InvalidOperationException($"No valid constructor (int handle) found to initialize {pType.Name}");

						ParameterExpression parameter = Expression.Parameter(typeof(int), "handle");
						NewExpression expression = Expression.New(constructor, parameter);

						var activator = Expression.Lambda<ConstructorCustomActivator<ISource>>(expression, parameter).Compile();
						parameters.Add(activator(source));
					}
					else if (typeof(Player).IsAssignableFrom(pType))
					{
						parameters.Add(EventHub.Instance.GetPlayers[source]);
					}
					else if (pType == typeof(string))
					{
						parameters.Add(source.ToString());
					}
					else if (pType == typeof(int))
					{
						parameters.Add(source);
					}
				}

				if (message.Parameters != null && message.Parameters.Count() > 0)
				{
					EventParameter[] array = message.Parameters.ToArray();

					for (int idx = startingIndex; idx < parameterInfos.Length; idx++)
					{
						ParameterInfo paramInfo = parameterInfos[idx];
						Type pType = paramInfo.ParameterType;
						int paramOffset = idx - startingIndex;

						if (paramOffset < array.Length)
						{
							EventParameter parameter = array[paramOffset];
							using SerializationContext context = new(message.Endpoint, $"(Process) Parameter Index {paramOffset}", Serialization, parameter.Data);

							if (TypeCache.IsSimpleType(pType))
								parameters.Add(TypeConvert.GetNewHolder(context, pType));
							else
								parameters.Add(context.Deserialize(pType));
						}
						else
						{
							parameters.Add(paramInfo.HasDefaultValue ? paramInfo.DefaultValue : (pType.IsValueType ? Activator.CreateInstance(pType) : null));
						}
					}
				}

				try
				{
					return @delegate.DynamicInvoke([.. parameters]);
				}
				catch (Exception ex)
				{
					if (isServer && hasSourceAttribute && parameters.Count > 0)
						parameters.RemoveAt(0);

					Logger.Error($"Handler [{message.Endpoint}] with parameters [{parameters.ToJson()}] threw an error.\n{ex}");
					return null;
				}
			}

			if (message.Flow == EventFlowType.Circular)
			{
				StopwatchUtil stopwatch = StopwatchUtil.StartNew();
				EventEntry subscription = _handlers[message.Endpoint];

				if (subscription.m_callbacks.Count != 1)
				{
					throw new EventException(subscription.m_callbacks.Count > 1
						? $"Found multiple callback handlers for event {message.Endpoint}, only 1 allowed."
						: $"Callback handler for event {message.Endpoint} not found.");
				}

				var (callback, binding) = subscription.m_callbacks[0];
				if (!CanExecuteEvent(binding, message.Sender))
					return;

				object? result = InvokeDelegate(callback);

				if (result is Task taskResult)
				{
					Task timeoutTask = DelayDelegate != null ? DelayDelegate(10000) : Task.Delay(10000);
					Task completed = await Task.WhenAny(taskResult, timeoutTask);

					if (completed == taskResult)
					{
#if CLIENT
						await taskResult;
#elif SERVER
						await taskResult.ConfigureAwait(false);
#endif
						PropertyInfo? resultProp = taskResult.GetType().GetProperty("Result");
						result = resultProp?.GetValue(taskResult);
					}
					else
					{
						throw new EventTimeoutException(
							$"({message.Endpoint} - {callback.Method.DeclaringType?.Name ?? "null"}/{callback.Method.Name}) The operation timed out.");
					}
				}

				Type resultType = result?.GetType() ?? typeof(object);
				EventResponseMessage response = new(message.Id, message.Endpoint, null);

				if (result != null)
				{
					using SerializationContext context = new(message.Endpoint, "(Process) Result", Serialization);
					context.Serialize(resultType, result);
					response.Data = context.GetData();
				}
				else
				{
					response.Data = [];
				}

				byte[] data = response.EncryptObject(source);
				PushDelegate?.Invoke(OutboundPipeline, source, message.Endpoint, binding, data);

				if (EventHub.Debug)
					Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
			}
			else
			{
				if (_handlers.TryGetValue(message.Endpoint, out EventEntry? entry))
				{
					foreach (var (callback, binding) in entry.m_callbacks)
					{
						if (CanExecuteEvent(binding, message.Sender))
							InvokeDelegate(callback);
					}
				}
			}
		}

		private bool CanExecuteEvent(Binding handler, EventRemote sender)
		{
			if (handler == Binding.None) return false;
			if (handler == Binding.All) return true;

			return (handler == Binding.Remote && sender == EventRemote.Client && isServer) ||
				   (handler == Binding.Remote && sender == EventRemote.Server && !isServer) ||
				   (handler == Binding.Local); // Binding.Local è sempre eseguibile in locale (sia Server che Client)
		}


		public void ProcessReply(byte[] serialized)
		{
			EventResponseMessage response = serialized.DecryptObject<EventResponseMessage>();
			ProcessReply(response);
		}

		public void ProcessReply(EventResponseMessage response)
		{
			EventObservable waiting = _queue.SingleOrDefault(self => self.Message.Id == response.Id)
				?? throw new InvalidOperationException($"No request matching {response.Id} was found for event {response.Endpoint}.");

			_queue.Remove(waiting);
			waiting.Callback.Invoke(response.Data);
		}

		internal async Task<EventMessage?> CreateAndSendAsync(EventFlowType flow, int source, string endpoint, Binding binding, params object[] args)
		{
			try
			{
				StopwatchUtil stopwatch = StopwatchUtil.StartNew();
				List<EventParameter> parameters = new(args.Length);

				for (int idx = 0; idx < args.Length; idx++)
				{
					object argument = args[idx];
					using SerializationContext context = new(endpoint, $"(Send) Parameter Index '{idx}'", Serialization);

					context.Serialize(argument.GetType(), argument);
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

				byte[] data = binding == Binding.Local && isServer
					? message.ToBytes()
					: message.EncryptObject(source);

				PushDelegate?.Invoke(InboundPipeline, source, endpoint, binding, data);

				if (EventHub.Debug)
				{
#if CLIENT
					Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : GetPlayerName(source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
					Logger.Debug($"[{endpoint} {flow}] Sent {data.Length} byte(s) to {(source == -1 ? "Server" : GetPlayerName("" + source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#endif
				}
				return message;
			}
			catch (Exception ex)
			{
				Logger.Error($"{endpoint} - {ex}");
				return new EventMessage(endpoint, flow, [], isServer ? EventRemote.Server : EventRemote.Client);
			}
		}

		internal async Task<EventMessage?> CreateAndSendLatentAsync(EventFlowType flow, int source, string endpoint, int bytePerSecond, params object[] args)
		{
			StopwatchUtil stopwatch = StopwatchUtil.StartNew();
			List<EventParameter> parameters = new(args.Length);

			for (int idx = 0; idx < args.Length; idx++)
			{
				object argument = args[idx];
				using SerializationContext context = new(endpoint, $"(Send) Parameter Index '{idx}'", Serialization);

				context.Serialize(argument.GetType(), argument);
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
			PushDelegateLatent?.Invoke(InboundPipeline, source, bytePerSecond, message.Endpoint, data);

			if (EventHub.Debug)
			{
#if CLIENT
				Logger.Debug($"[{endpoint} {flow}] Sent latent {data.Length} byte(s) to {(source == -1 ? "Server" : GetPlayerName(source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#elif SERVER
				Logger.Debug($"[{endpoint} {flow}] Sent latent {data.Length} byte(s) to {(source == -1 ? "Server" : GetPlayerName("" + source))} in {stopwatch.Elapsed.TotalMilliseconds}ms");
#endif
			}
			return message;
		}

		protected async Task<T?> GetInternal<T>(int source, string endpoint, Binding binding, params object[] args)
		{
			StopwatchUtil stopwatch = StopwatchUtil.StartNew();
			EventMessage? message = await CreateAndSendAsync(EventFlowType.Circular, source, endpoint, binding, args);
			if (message == null) return default;

			EventValueHolder<T> holder = new();
			TaskCompletionSource<bool> tokenLoading = new();

			_queue.Add(new EventObservable(message, data =>
			{
				using SerializationContext context = new(endpoint, "(Get) Response", Serialization, data);

				holder.Data = data;
				holder.Value = context.Deserialize<T>();

				tokenLoading.SetResult(true);
			}));

			await tokenLoading.Task;

			double elapsed = stopwatch.Elapsed.TotalMilliseconds;
			if (EventHub.Debug)
			{
#if CLIENT
				Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : GetPlayerName(source))} of {holder.Data.Length} byte(s) in {elapsed}ms");
#elif SERVER
				Logger.Debug($"[{message.Endpoint} {EventFlowType.Circular}] Received response from {(source == -1 ? "Server" : GetPlayerName("" + source))} of {holder.Data.Length} byte(s) in {elapsed}ms");
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