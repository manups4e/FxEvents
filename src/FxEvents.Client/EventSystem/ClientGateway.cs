using CitizenFX.FiveM.Shared;
using FxEvents.Shared;
using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.Snowflakes;
using System;
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
	internal class ClientGateway : BaseGateway
	{
		protected override ISerialization Serialization { get; }

		private EventHub Hub => EventHub.Instance;
		private readonly Curve25519 _curve25519;
		private byte[] _secret = [];

		public ClientGateway()
		{
			SnowflakeGenerator.Create((short)Random.Shared.Next(1, 199));
			_curve25519 = Curve25519.Create();
			Serialization = new MsgPackSerialization();
			DelayDelegate = async delay => await API.Delay(delay);
			PrepareDelegate = PrepareAsync;
			PushDelegate = Push;
			PushDelegateLatent = PushLatent;
		}

		internal void AddEvents()
		{
			Hub.AddEventHandler(InboundPipeline, new Action<string, bool, byte[]>(async (endpoint, isRemote, encrypted) =>
			{
				try
				{
					await ProcessInboundAsync(new ServerId().Handle, endpoint, isRemote, encrypted);
				}
				catch (Exception ex)
				{
					EventMessage message = encrypted.DecryptObject<EventMessage>();
					Logger.Error($"InboundPipeline [{message.Endpoint}]: {ex}");
				}
			}));

			Hub.AddEventHandler(OutboundPipeline, new Action<string, bool, byte[]>((endpoint, isRemote, serialized) =>
			{
				try
				{
					ProcessReply(serialized);
				}
				catch (Exception ex)
				{
					Logger.Error($"OutboundPipeline: {ex}");
				}
			}));

			Hub.AddEventHandler(SignaturePipeline, new Action<byte[]>(signature => _secret = _curve25519.GetSharedSecret(signature)));
			API.EmitServer(SignaturePipeline, _curve25519.GetPublicKey());
		}

		internal async Task PrepareAsync(string pipeline, int source, IMessage message)
		{
			if (_secret.Length == 0)
			{
				StopwatchUtil stopwatch = StopwatchUtil.StartNew();
				while (_secret.Length == 0) await API.Delay(0);
				if (EventHub.Debug)
				{
					Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
				}
			}
		}

		internal void Push(string pipeline, int source, string endpoint, bool isRemote, byte[] buffer)
		{
			if (isRemote)
			{
				if (source != -1) throw new InvalidOperationException($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
				API.EmitServer(pipeline, endpoint, isRemote, buffer);
			}
			else
			{
				API.EmitLocal(pipeline, endpoint, isRemote, buffer);
			}
		}

		internal void PushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer)
		{
			if (source != -1) throw new InvalidOperationException($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
			API.EmitServerLatent(bytePerSecond, pipeline, endpoint, true, buffer);
		}

		public async void SendNet(string endpoint, params object[] args)
		{
			await CreateAndSendAsync(EventFlowType.Straight, new ServerId().Handle, endpoint, isRemote: true, args);
		}

		public async void SendLocal(string endpoint, params object[] args)
		{
			await CreateAndSendAsync(EventFlowType.Straight, new ServerId().Handle, endpoint, isRemote: false, args);
		}

		public async void SendLatent(string endpoint, int bytePerSecond, params object[] args)
		{
			await CreateAndSendLatentAsync(EventFlowType.Straight, new ServerId().Handle, endpoint, bytePerSecond, args);
		}

		public async Task<T?> GetNet<T>(string endpoint, params object[] args)
		{
			return await GetInternal<T>(new ServerId().Handle, endpoint, isRemote: true, args);
		}

		public async Task<T?> GetLocal<T>(string endpoint, params object[] args)
		{
			return await GetInternal<T>(new ServerId().Handle, endpoint, isRemote: false, args);
		}

		internal byte[] GetSecret(int _)
		{
			if (_secret == null || _secret.Length == 0)
				throw new InvalidOperationException("Shared Encryption Secret has not been generated yet");
			return _secret;
		}
	}
}