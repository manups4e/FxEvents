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
	public delegate Task EventDelayMethod(int ms = 0);
	public delegate Task EventMessagePreparation(string pipeline, int source, IMessage message);
	public delegate void EventMessagePush(string pipeline, int source, string endpoint, bool isRemote, byte[] buffer);
	public delegate void EventMessagePushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer);
	public delegate ISource ConstructorCustomActivator<T>(int handle);

	public abstract class BaseGateway
	{
		internal Log Logger = new();
		internal string InboundPipeline = string.Empty;
		internal string OutboundPipeline = string.Empty;
		internal string SignaturePipeline = string.Empty;
		internal bool takesSource = false;

#if CLIENT
        internal bool isServer = false;
#elif SERVER
		internal bool isServer = true;
#endif

		protected abstract ISerialization Serialization { get; }

		private readonly HashSet<Snowflake> _processedEventIds = [];
		private readonly Queue<Snowflake> _eventIdsOrder = new();
		private readonly List<EventObservable> _queue = [];
		internal EventsDictionary _handlers = new();

		public EventDelayMethod? DelayDelegate { get; set; }
		public EventMessagePreparation? PrepareDelegate { get; set; }
		public EventMessagePush? PushDelegate { get; set; }
		public EventMessagePushLatent? PushDelegateLatent { get; set; }

		public async Task ProcessInboundAsync(int source, string endpoint, bool isRemote, byte[] serialized)
		{
			EventMessage message;
			try
			{
				message = !isRemote && isServer
					? serialized.FromBytes<EventMessage>()
					: serialized.DecryptObject<EventMessage>(source);

				lock (_processedEventIds)
				{
					if (!_processedEventIds.Add(message.Id))
					{
#if CLIENT
                        API.EmitServer("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
                        Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {GetPlayerName(source)} [{source}] has a used ID");
#elif SERVER
						API.EmitLocal("fxevents:tamperingprotection", source, endpoint, TamperType.REPEATED_MESSAGE_ID);
						Logger.Warning($"Possible tampering detected, the event \"{endpoint}\" sent by player {GetPlayerName("" + source)} [{source}] has a used ID");
#endif
						return;
					}

					_eventIdsOrder.Enqueue(message.Id);
					if (_eventIdsOrder.Count > 500)
					{
						_processedEventIds.Remove(_eventIdsOrder.Dequeue());
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
						Player? player = API.Players.Get(source);
						parameters.Add(player);
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

				if (message.Parameters != null && message.Parameters.Count > 0)
				{
					EventParameter[] array = [.. message.Parameters];

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

				var (callback, isRemote) = subscription.m_callbacks[0];
				if (!CanExecuteEvent(isRemote, message.Sender))
					return;

				object? result = InvokeDelegate(callback);

				if (result is Task taskResult)
				{
					try
					{
						TimeSpan timeout = TimeSpan.FromMilliseconds(10000);
#if CLIENT
                        await taskResult.WaitAsync(timeout);
#elif SERVER
						await taskResult.WaitAsync(timeout).ConfigureAwait(false);
#endif
						Type taskType = taskResult.GetType();
						result = taskType.IsGenericType ? ((dynamic)taskResult).Result : null;
					}
					catch (TimeoutException)
					{
						throw new EventTimeoutException(
							$"({message.Endpoint} - {callback.Method.DeclaringType?.Name ?? "null"}/{callback.Method.Name}) The operation timed out after 10s.");
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

				byte[] data = isRemote && isServer ? response.EncryptObject(source) : response.ToBytes();
				PushDelegate?.Invoke(OutboundPipeline, source, message.Endpoint, isRemote, data);

				if (EventHub.Debug)
					Logger.Debug($"[{message.Endpoint}] Responded to {source} with {data.Length} byte(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
			}
			else
			{
				if (_handlers.TryGetValue(message.Endpoint, out EventEntry? entry))
				{
					foreach (var (callback, isRemote) in entry.m_callbacks)
					{
						if (CanExecuteEvent(isRemote, message.Sender))
							InvokeDelegate(callback);
					}
				}
			}
		}

		private bool CanExecuteEvent(bool isRemoteHandler, EventRemote sender)
		{
			if (isRemoteHandler)
				return (isServer && sender == EventRemote.Client) || (!isServer && sender == EventRemote.Server);
			return (isServer && sender == EventRemote.Server) || (!isServer && sender == EventRemote.Client);
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

		internal async Task<EventMessage?> CreateAndSendAsync(EventFlowType flow, int source, string endpoint, bool isRemote, params object[] args)
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

				if (EventHub.Gateway.GetSecret(source).Length == 0 && isRemote) return null;

				byte[] data = !isRemote
					? message.ToBytes()
					: message.EncryptObject(source);

				PushDelegate?.Invoke(InboundPipeline, source, endpoint, isRemote, data);

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

		protected async Task<T?> GetInternal<T>(int source, string endpoint, bool isRemote, params object[] args)
		{
			StopwatchUtil stopwatch = StopwatchUtil.StartNew();
			EventMessage? message = await CreateAndSendAsync(EventFlowType.Circular, source, endpoint, isRemote, args);
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

		public void MountNet(string endpoint, Delegate @delegate)
		{
			if (EventHub.Debug) Logger.Debug($"Mounted Net Event: {endpoint}");
			_handlers.Add(endpoint, isRemote: true, @delegate);
		}

		public void MountLocal(string endpoint, Delegate @delegate)
		{
			if (EventHub.Debug) Logger.Debug($"Mounted Local Event: {endpoint}");
			_handlers.Add(endpoint, isRemote: false, @delegate);
		}

		public void Unmount(string endpoint)
		{
			_handlers.Remove(endpoint);
		}
	}
}