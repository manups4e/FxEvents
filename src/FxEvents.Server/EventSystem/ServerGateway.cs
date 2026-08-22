using CitizenFX.Core;
using CitizenFX.FiveM.Server.Entities;
using CitizenFX.FiveM.Shared.Serialization;
using FxEvents.Shared;
using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.TypeExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
	public class ServerGateway : BaseGateway
	{
		protected override ISerialization Serialization { get; }
		internal readonly Dictionary<int, byte[]> _signatures = [];

		private EventHub Hub => EventHub.Instance;

		public ServerGateway()
		{
			SnowflakeGenerator.Create((short)Random.Shared.Next(200, 399));
			Serialization = new MsgPackSerialization();
			DelayDelegate = async delay => await API.Delay(delay);
			PrepareDelegate = PrepareAsync;
			PushDelegate = Push;
			PushDelegateLatent = PushLatent;
		}

		internal void AddEvents()
		{
			Hub.RegisterEvent(SignaturePipeline, new Action<int, byte[]>(GetSignature));
			Hub.RegisterEvent(InboundPipeline, new Action<int, string, bool, byte[]>(Inbound));
			Hub.RegisterEvent(OutboundPipeline, new Action<int, string, bool, byte[]>(Outbound));
		}

		internal void Push(string pipeline, int source, string endpoint, bool isRemote, byte[] buffer)
		{
			if (isRemote)
			{
				if (source != new ServerId().Handle)
					API.EmitClient(source, pipeline, endpoint, isRemote, buffer);
				else
					API.EmitClient(-1, pipeline, endpoint, isRemote, buffer);
			}
			else
			{
				API.EmitLocal(pipeline, endpoint, isRemote, buffer);
			}
		}

		internal void PushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer)
		{
			if (source != new ServerId().Handle)
				API.EmitClient(source, pipeline, bytePerSecond, endpoint, true, buffer);
			else
				API.EmitClientLatent(source, bytePerSecond, pipeline, endpoint, true, buffer);
		}

		private void GetSignature([FromSource] int source, byte[] clientPubKey)
		{
			try
			{
				int client = source;
				if (_signatures.ContainsKey(client))
				{
					Logger.Warning($"Client {GetPlayerName("" + client)}[{client}] tried acquiring event signature more than once.");
					API.EmitLocal("fxevents:tamperingprotection", source, "signature retrieval", TamperType.REQUESTED_NEW_PUBLIC_KEY);
					return;
				}

				Curve25519 curve25519 = Curve25519.Create();
				byte[] secret = curve25519.GetSharedSecret(clientPubKey);

				_signatures.Add(client, secret);

				API.EmitClient(client, SignaturePipeline, curve25519.GetPublicKey());
			}
			catch (Exception ex)
			{
				Logger.Error(ex.ToString());
			}
		}

		private async void Inbound([FromSource] int source, string endpoint, bool isRemote, byte[] encrypted)
		{
			try
			{
				if (source != -1 && isRemote)
				{
					if (!_signatures.ContainsKey(source))
						return;
				}

				try
				{
					await ProcessInboundAsync(source, endpoint, isRemote, encrypted);
				}
				catch (TimeoutException)
				{
					DropPlayer(source.ToString(), $"Operation timed out: {endpoint.ToBase64()}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error(ex.ToString());
			}
		}

		private void Outbound([FromSource] int source, string endpoint, bool isRemote, byte[] encrypted)
		{
			try
			{
				int client = source;

				if (!_signatures.TryGetValue(client, out _)) return;

				EventResponseMessage response = encrypted.DecryptObject<EventResponseMessage>(client);

				ProcessReply(response);
			}
			catch (Exception ex)
			{
				Logger.Error(ex.ToString());
			}
		}

		#region Send Net API
		public void SendNet(Player player, string endpoint, params object[] args) => SendNet(Convert.ToInt32(player.Handle), endpoint, args);
		public void SendNet(ISource client, string endpoint, params object[] args) => SendNet(client.Handle, endpoint, args);
		public void SendNet(List<Player> players, string endpoint, params object[] args) => SendNet(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
		public void SendNet(List<ISource> clients, string endpoint, params object[] args) => SendNet(clients.Select(x => x.Handle).ToList(), endpoint, args);

		public async void SendNet(List<int> targets, string endpoint, params object[] args)
		{
			foreach (int target in targets)
			{
				await API.Delay(0);
				SendNet(target, endpoint, args);
			}
		}

		public async void SendNet(int target, string endpoint, params object[] args)
		{
			bool isValidTarget = target >= 0 && API.Players.All.Any(p => p.Handle == target);

			if (isValidTarget)
			{
				await CreateAndSendAsync(EventFlowType.Straight, target, endpoint, isRemote: true, args);
			}
		}
		#endregion

		#region Send Local API
		public async void SendLocal(string endpoint, params object[] args)
		{
			await CreateAndSendAsync(EventFlowType.Straight, -1, endpoint, isRemote: false, args);
		}
		#endregion

		#region Send Latent API
		public void SendLatent(Player player, string endpoint, int bytesxSecond, params object[] args) => SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesxSecond, args);
		public void SendLatent(ISource client, string endpoint, int bytesxSecond, params object[] args) => SendLatent(client.Handle, endpoint, bytesxSecond, args);
		public void SendLatent(List<Player> players, string endpoint, int bytesxSecond, params object[] args) => SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesxSecond, args);
		public void SendLatent(List<ISource> clients, string endpoint, int bytesxSecond, params object[] args) => SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesxSecond, args);

		public async void SendLatent(List<int> targets, string endpoint, int bytesxSecond, params object[] args)
		{
			foreach (int target in targets)
			{
				await API.Delay(0);
				SendLatent(target, endpoint, bytesxSecond, args);
			}
		}

		public async void SendLatent(int target, string endpoint, int bytesxSecond, params object[] args)
		{
			if (target >= 0 && API.Players.All.Any(p => p.Handle == target))
				await CreateAndSendLatentAsync(EventFlowType.Straight, target, endpoint, bytesxSecond, args);
		}
		#endregion

		#region Request / Response (Get) API
		public Task<T?> GetNet<T>(Player player, string endpoint, params object[] args) =>
			GetNet<T>(Convert.ToInt32(player.Handle), endpoint, args);

		public Task<T?> GetNet<T>(ISource client, string endpoint, params object[] args) =>
			GetNet<T>(client.Handle, endpoint, args);

		public async Task<T?> GetNet<T>(int target, string endpoint, params object[] args)
		{
			return await GetInternal<T>(target, endpoint, isRemote: true, args);
		}

		public async Task<T?> GetLocal<T>(string endpoint, params object[] args)
		{
			return await GetInternal<T>(-1, endpoint, isRemote: false, args);
		}
		#endregion

		internal async Task PrepareAsync(string pipeline, int source, IMessage message)
		{
			if (GetSecret(source).Length == 0)
			{
				StopwatchUtil stopwatch = StopwatchUtil.StartNew();
				long time = GetGameTimer();
				while (GetSecret(source).Length == 0)
				{
					if (GetGameTimer() - time > 1000)
					{
						if (EventHub.Debug)
						{
							Logger.Debug($"[{message}] Took too much time: {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval, client not found, still connecting or disconnected.");
						}
						return;
					}
					await API.Delay(0);
				}
				if (EventHub.Debug)
				{
					Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
				}
			}
		}

		internal byte[] GetSecret(int source)
		{
			if (!_signatures.TryGetValue(source, out byte[]? secret))
			{
				Curve25519 curve25519 = Curve25519.Create();
				return curve25519.GetSharedSecret(curve25519.GetPublicKey());
			}
			return secret;
		}
	}
}