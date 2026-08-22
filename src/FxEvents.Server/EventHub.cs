global using CitizenFX.FiveM.Server;
global using CitizenFX.FiveM.Shared.Script;
global using static CitizenFX.FiveM.Server.Native;
using CitizenFX.FiveM.Server.Entities;
using CitizenFX.FiveM.Shared;
using CitizenFX.FiveM.Shared.Serialization;
using FxEvents.EventSystem;
using FxEvents.Shared;
using FxEvents.Shared.Attributes;
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
	/// <summary>
	/// Central event management hub for FxEvents, handling encrypted network events and high-performance local dispatches.
	/// </summary>
	public class EventHub : IScript
	{
		internal static Log Logger { get; set; } = new();
		internal static ServerGateway Gateway { get; set; }
		internal static bool Debug { get; set; }

		/// <summary>
		/// Gets a value indicating whether the <see cref="EventHub"/> framework has been initialized.
		/// </summary>
		public static bool Initialized { get; private set; } = false;

		internal static EventHub Instance { get; private set; }

		/// <summary>
		/// Gets the active collection of registered event endpoints and their bound delegates.
		/// </summary>
		public static EventsDictionary Events => Gateway._handlers;

		/// <summary>
		/// Explicitly initializes the <see cref="EventHub"/> framework, setting up crypto pipelines and registering event attributes.
		/// </summary>
		/// <remarks>
		/// Calling this method manually at resource startup is optional, as all public API methods automatically invoke it if uninitialized.
		/// </remarks>
		public static void Initialize()
		{
			if (Initialized) return;

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
				var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

				foreach (var method in methods)
				{
					var netAttr = method.GetCustomAttribute<FxNetEventAttribute>();
					var localAttr = method.GetCustomAttribute<FxLocalEventAttribute>();

					if (netAttr == null && localAttr == null) continue;

					string eventName = netAttr?.Name ?? localAttr!.Name;
					bool isRemote = netAttr != null;

					if (!method.IsStatic)
					{
						Logger.Error($"Error registering method {method.Name} - FxEvents supports only Static methods!");
						continue;
					}

					var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
					var actionType = Expression.GetDelegateType([.. parameters, method.ReturnType]);

					if (method.ReturnType != typeof(void))
					{
						if (!withReturnType.Add(eventName))
						{
							throw new InvalidOperationException($"FxEvents - Cannot register more than 1 delegate with a return type for [{eventName}]!");
						}
					}

					var @delegate = Delegate.CreateDelegate(actionType, method);

					if (isRemote)
						OnNet(eventName, @delegate);
					else
						OnLocal(eventName, @delegate);
				}
			}

			WarmUpSerialization();
		}

		internal void RegisterEvent(string eventName, Delegate action)
		{
			EnsureInitialized();
			SharedAPI.OnNetEvent(eventName, action);
		}

		#region SendNet Methods
		/// <summary>
		/// Sends an encrypted network message to a specific target player.
		/// </summary>
		/// <param name="player">The target <see cref="Player"/> instance.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNet(Player player, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(player, endpoint, args);
		}

		/// <summary>
		/// Sends an encrypted network message to a specific client source.
		/// </summary>
		/// <param name="client">The target <see cref="ISource"/> client wrapper.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNet(ISource client, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(client, endpoint, args);
		}

		/// <summary>
		/// Sends an encrypted network message to a collection of target players.
		/// </summary>
		/// <param name="players">The collection of target <see cref="Player"/> instances.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNet(IEnumerable<Player> players, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(players.Select(p => Convert.ToInt32(p.Handle)).ToList(), endpoint, args);
		}

		/// <summary>
		/// Sends an encrypted network message to a collection of client sources.
		/// </summary>
		/// <param name="clients">The collection of target <see cref="ISource"/> client wrappers.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNet(IEnumerable<ISource> clients, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(clients.Select(c => c.Handle).ToList(), endpoint, args);
		}

		/// <summary>
		/// Sends an encrypted network message to a target player specified by handle ID.
		/// </summary>
		/// <param name="target">The target player's server handle ID.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNet(int target, string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(target, endpoint, args);
		}

		/// <summary>
		/// Broadcasts an encrypted network message to all connected clients on the server.
		/// </summary>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and pass to the event handler.</param>
		public static void SendNetToAll(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(API.Players.All.Select(p => Convert.ToInt32(p.Handle)).ToList(), endpoint, args);
		}
		#endregion

		#region SendLocal Methods
		/// <summary>
		/// Dispatches an unencrypted local event message within the current process/AppDomain.
		/// </summary>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass to the local handler.</param>
		public static void SendLocal(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLocal(endpoint, args);
		}
		#endregion

		#region SendLatent Methods
		/// <summary>
		/// Sends a rate-limited, bandwidth-throttled network event to a specific target player.
		/// </summary>
		/// <param name="player">The target <see cref="Player"/> instance.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="bytesPerSeconds">Maximum rate of data transfer in bytes per second.</param>
		/// <param name="args">Optional arguments to serialize and send.</param>
		public static void SendLatent(Player player, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesPerSeconds, args);
		}

		/// <summary>
		/// Sends a rate-limited, bandwidth-throttled network event to a specific client source.
		/// </summary>
		/// <param name="client">The target <see cref="ISource"/> client wrapper.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="bytesPerSeconds">Maximum rate of data transfer in bytes per second.</param>
		/// <param name="args">Optional arguments to serialize and send.</param>
		public static void SendLatent(ISource client, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(client.Handle, endpoint, bytesPerSeconds, args);
		}

		/// <summary>
		/// Sends a rate-limited, bandwidth-throttled network event to a collection of target players.
		/// </summary>
		/// <param name="players">The collection of target <see cref="Player"/> instances.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="bytesPerSeconds">Maximum rate of data transfer in bytes per second.</param>
		/// <param name="args">Optional arguments to serialize and send.</param>
		public static void SendLatent(IEnumerable<Player> players, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
		}

		/// <summary>
		/// Sends a rate-limited, bandwidth-throttled network event to a collection of client sources.
		/// </summary>
		/// <param name="clients">The collection of target <see cref="ISource"/> client wrappers.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="bytesPerSeconds">Maximum rate of data transfer in bytes per second.</param>
		/// <param name="args">Optional arguments to serialize and send.</param>
		public static void SendLatent(IEnumerable<ISource> clients, string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesPerSeconds, args);
		}
		#endregion

		#region Request / Response (Get) Methods
		/// <summary>
		/// Asynchronously sends a network request to a target player and awaits a typed response.
		/// </summary>
		/// <typeparam name="T">The expected response payload type.</typeparam>
		/// <param name="player">The target <see cref="Player"/> instance.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass with the request.</param>
		/// <returns>A task containing the returned payload of type <typeparamref name="T"/>, or <c>null</c> if the request failed.</returns>
		public static async Task<T?> GetNet<T>(Player player, string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.GetNet<T>(Convert.ToInt32(player.Handle), endpoint, args);
		}

		/// <summary>
		/// Asynchronously sends a network request to a client source and awaits a typed response.
		/// </summary>
		/// <typeparam name="T">The expected response payload type.</typeparam>
		/// <param name="client">The target <see cref="ISource"/> client wrapper.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass with the request.</param>
		/// <returns>A task containing the returned payload of type <typeparamref name="T"/>, or <c>null</c> if the request failed.</returns>
		public static async Task<T?> GetNet<T>(ISource client, string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.GetNet<T>(client.Handle, endpoint, args);
		}

		/// <summary>
		/// Asynchronously sends a network request to a target player handle ID and awaits a typed response.
		/// </summary>
		/// <typeparam name="T">The expected response payload type.</typeparam>
		/// <param name="target">The target player's server handle ID.</param>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass with the request.</param>
		/// <returns>A task containing the returned payload of type <typeparamref name="T"/>, or <c>null</c> if the request failed.</returns>
		public static async Task<T?> GetNet<T>(int target, string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.GetNet<T>(target, endpoint, args);
		}

		/// <summary>
		/// Asynchronously invokes an in-process local event handler and awaits a typed response.
		/// </summary>
		/// <typeparam name="T">The expected response payload type.</typeparam>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass with the request.</param>
		/// <returns>A task containing the returned payload of type <typeparamref name="T"/>, or <c>null</c> if the request failed.</returns>
		public static async Task<T?> GetLocal<T>(string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.GetLocal<T>(endpoint, args);
		}
		#endregion

		#region Registration (On / Off) API
		/// <summary>
		/// Subscribes a delegate callback to an encrypted network event endpoint.
		/// </summary>
		/// <param name="endpoint">The event endpoint name to listen for.</param>
		/// <param name="delegate">The target delegate callback method.</param>
		public static void OnNet(string endpoint, Delegate @delegate)
		{
			EnsureInitialized();
			Gateway.MountNet(endpoint, @delegate);
		}

		/// <summary>
		/// Subscribes a delegate callback to an unencrypted local event endpoint.
		/// </summary>
		/// <param name="endpoint">The event endpoint name to listen for.</param>
		/// <param name="delegate">The target delegate callback method.</param>
		public static void OnLocal(string endpoint, Delegate @delegate)
		{
			EnsureInitialized();
			Gateway.MountLocal(endpoint, @delegate);
		}

		/// <summary>
		/// Unregisters and unmounts all callbacks associated with the specified endpoint name.
		/// </summary>
		/// <param name="endpoint">The event endpoint name to unregister.</param>
		public static void Off(string endpoint)
		{
			EnsureInitialized();
			Gateway.Unmount(endpoint);
		}
		#endregion

		private void OnPlayerDropped([FromSource] Player player)
		{
			Gateway._signatures.Remove(player.Handle);
		}
	}
}