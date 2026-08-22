global using CitizenFX.FiveM.Server;
global using CitizenFX.FiveM.Shared.Script;
global using static CitizenFX.FiveM.Server.Native;
using CitizenFX.FiveM.Server.Entities;
using CitizenFX.FiveM.Shared;
using CitizenFX.FiveM.Shared.Serialization;
using FxEvents.EventSystem;
using FxEvents.Shared;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.EventSubsystem;

using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace FxEvents
{
	public class EventHub : IScript
	{
		internal static Log Logger { get; set; } = new();
		internal static ServerGateway Gateway { get; set; }
		internal static bool Debug { get; set; }
		public static bool Initialized { get; private set; } = false;
		internal static EventHub Instance { get; private set; }

		public static EventsDictionary Events => Gateway._handlers;

		/// <summary>
		/// Inizializza l'EventHub. Può essere chiamato manualmente all'avvio del la risorsa.
		/// </summary>
		public static void Initialize()
		{
			// Se è già inizializzato (o in fase di inizializzazione), esce subito per spezzare il loop
			if (Initialized) return;

			// Imposta la flag IMMEDIATAMENTE prima di eseguire qualsiasi registrazione
			Initialized = true;

			Instance ??= new EventHub();

			var resName = GetCurrentResourceName();
			string debugMode = GetResourceMetadata(resName, "fxevents_debug_mode", 0);
			Debug = debugMode == "yes" || debugMode == "true" || (int.TryParse(debugMode, out int num) && num > 0);

			byte[] inbound = Encryption.GenerateHash(resName + "_inbound");
			byte[] outbound = Encryption.GenerateHash(resName + "_outbound");
			byte[] signature = Encryption.GenerateHash(resName + "_signature");

			Gateway = new ServerGateway
			{
				SignaturePipeline = signature.BytesToString(),
				InboundPipeline = inbound.BytesToString(),
				OutboundPipeline = outbound.BytesToString()
			};

			SharedAPI.OnEvent("playerJoining", new Action<Player>(Instance.OnPlayerDropped));
			SharedAPI.OnEvent("playerDropped", new Action<Player>(Instance.OnPlayerDropped));

			InitializeInternal();
		}

		private static void WarmUpSerialization()
		{
			// Forza il JIT a compilare i formattatori di MessagePack all'avvio della risorsa
			_ = BinaryHelper.ToBytes(new EventMessage());
		}

		private static void EnsureInitialized()
		{
			if (!Initialized)
			{
				Initialize();
			}
		}

		[OnCommand("generatekey", Restricted = true)]
		private static async Task GenerateKeyCommand()
		{
			Logger.Info("Generating random passphrase with a 50 words dictionary...");
			var ret = await Encryption.GenerateKey();
			string print = $"Here is your generated encryption key, save it in a safe place.\n" +
						   $"This key is not saved by FXEvents anywhere, so please store it somewhere safe. If you save encrypted data and lose this key, your data will be lost.\n" +
						   $"You can always generate new keys by using \"generatekey\" command.\n" +
						   $"Passphrase: {ret.Item1}\nEncrypted Passphrase: {ret.Item2}";
			Logger.Info(print);
		}

		private static void InitializeInternal()
		{
			Gateway.AddEvents();
			var assembly = Assembly.GetCallingAssembly();
			HashSet<string> withReturnType = [];

			foreach (var type in assembly.GetTypes())
			{
				var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
								  .Where(m => m.GetCustomAttributes(typeof(FxEventAttribute), false).Length > 0);

				foreach (var method in methods)
				{
					var attribute = method.GetCustomAttribute<FxEventAttribute>();
					if (attribute == null) continue;

					var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
					var actionType = Expression.GetDelegateType([.. parameters, method.ReturnType]);

					if (method.ReturnType != typeof(void))
					{
						if (!withReturnType.Add(attribute.Name))
						{
							throw new InvalidOperationException($"FxEvents - Failed registering [{attribute.Name}] delegates. Cannot register more than 1 delegate for [{attribute.Name}] with a return type!");
						}
					}

					if (method.IsStatic)
					{
						Mount(attribute.Name, attribute.Binding, Delegate.CreateDelegate(actionType, method));
					}
					else
					{
						Logger.Error($"Error registering method {method.Name} - FxEvents supports only Static methods for its [FxEvent] attribute!");
					}
				}
			}

			//Note: This is to allow msgpack caching of EventMessage.. this will avoid the first event to take more than 100ms to send
			WarmUpSerialization();
		}

		internal async void RegisterEvent(string eventName, Delegate action)
		{
			EnsureInitialized();
			while (!Initialized) await API.Delay(0);
			SharedAPI.OnNetEvent(eventName, action);
		}

		#region Send Methods
		public static void Send(Player player, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(player, endpoint, args);
		}

		public static void Send(ISource client, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(client, endpoint, args);
		}

		public static void Send(IEnumerable<Player> players, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(players.ToList(), endpoint, args);
		}

		public static void Send(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(API.Players.All.ToList(), endpoint, args);
		}

		public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(clients.ToList(), endpoint, args);
		}

		public static void SendLocal(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(endpoint, args);
		}
		#endregion

		#region SendLatent Methods
		public static void SendLatent(Player player, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesPerSeconds, args);
		}

		public static void SendLatent(ISource client, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(client.Handle, endpoint, bytesPerSeconds, args);
		}

		public static void SendLatent(IEnumerable<Player> players, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
		}

		public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(API.Players.All.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
		}

		public static void SendLatent(IEnumerable<ISource> clients, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesPerSeconds, args);
		}
		#endregion

		#region Get Methods
		public static async Task<T?> Get<T>(Player player, string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.Get<T>(Convert.ToInt32(player.Handle), endpoint, args);
		}

		public static async Task<T?> Get<T>(ISource client, string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.Get<T>(client.Handle, endpoint, args);
		}
		#endregion

		public static void Mount(string endpoint, Binding binding, Delegate @delegate)
		{
			EnsureInitialized();
			Gateway.Mount(endpoint, binding, @delegate);
		}

		public static void Unmount(string endpoint)
		{
			EnsureInitialized();
			Gateway.Unmount(endpoint);
		}

		private void OnPlayerDropped([FromSource] Player player)
		{
			if (Gateway._signatures.ContainsKey(player.Handle))
				Gateway._signatures.Remove(player.Handle);
		}
	}
}