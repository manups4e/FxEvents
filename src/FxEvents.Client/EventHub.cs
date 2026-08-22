global using CitizenFX.FiveM.Client;
global using CitizenFX.FiveM.Shared.Script;
global using static CitizenFX.FiveM.Client.Native;
using CitizenFX.FiveM.Client.Entities;
using CitizenFX.FiveM.Shared;
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
	/// Client-side event management hub for FxEvents, handling encrypted network messaging and fast local dispatches.
	/// </summary>
	public class EventHub : IScript
	{
		internal static Log Logger { get; set; } = new();
		internal static ClientGateway Gateway { get; set; }
		internal static bool Debug { get; set; }

		/// <summary>
		/// Gets a value indicating whether the client-side <see cref="EventHub"/> framework has been initialized.
		/// </summary>
		public static bool Initialized { get; private set; } = false;

		internal static EventHub Instance { get; private set; }

		/// <summary>
		/// Gets the active collection of registered event endpoints and their bound delegates.
		/// </summary>
		public static EventsDictionary Events => Gateway._handlers;

		/// <summary>
		/// Explicitly initializes the client-side <see cref="EventHub"/> framework, setting up crypto pipelines and registering event attributes.
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

			Gateway = new ClientGateway
			{
				SignaturePipeline = signature.BytesToString(),
				InboundPipeline = inbound.BytesToString(),
				OutboundPipeline = outbound.BytesToString()
			};

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
						Logger.Error($"Error registering {method.Name}: FxEvents supports only static methods for event handlers!");
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

		internal void AddEventHandler(string eventName, Delegate action)
		{
			SharedAPI.OnNetEvent(eventName, action);
		}

		#region Public Static API

		/// <summary>
		/// Sends an encrypted network message from the client to the server.
		/// </summary>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to serialize and send to the server handler.</param>
		public static void SendNet(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendNet(endpoint, args);
		}

		/// <summary>
		/// Dispatches an unencrypted local event message within the client process/AppDomain.
		/// </summary>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass to the local client handler.</param>
		public static void SendLocal(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLocal(endpoint, args);
		}

		/// <summary>
		/// Sends a rate-limited, bandwidth-throttled network event from the client to the server.
		/// </summary>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="target">The target server handle ID (default is -1 for the server).</param>
		/// <param name="bytesPerSecond">Maximum rate of data transfer in bytes per second.</param>
		/// <param name="args">Optional arguments to serialize and send.</param>
		public static void SendLatent(string endpoint, int target, int bytesPerSecond, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(endpoint, target, bytesPerSecond, args);
		}

		/// <summary>
		/// Asynchronously sends a network request to the server and awaits a typed response payload.
		/// </summary>
		/// <typeparam name="T">The expected response payload type.</typeparam>
		/// <param name="endpoint">The registered event endpoint name.</param>
		/// <param name="args">Optional arguments to pass with the request.</param>
		/// <returns>A task containing the returned payload of type <typeparamref name="T"/>, or <c>null</c> if the request failed or timed out.</returns>
		public static async Task<T?> GetNet<T>(string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.GetNet<T>(endpoint, -1, args);
		}

		/// <summary>
		/// Asynchronously invokes an in-process local client event handler and awaits a typed response payload.
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

		/// <summary>
		/// Subscribes a delegate callback to an encrypted network event endpoint coming from the server.
		/// </summary>
		/// <param name="endpoint">The event endpoint name to listen for.</param>
		/// <param name="action">The target delegate callback method.</param>
		public static void OnNet(string endpoint, Delegate action)
		{
			EnsureInitialized();
			Gateway.MountNet(endpoint, action);
		}

		/// <summary>
		/// Subscribes a delegate callback to an unencrypted local client event endpoint.
		/// </summary>
		/// <param name="endpoint">The event endpoint name to listen for.</param>
		/// <param name="action">The target delegate callback method.</param>
		public static void OnLocal(string endpoint, Delegate action)
		{
			EnsureInitialized();
			Gateway.MountLocal(endpoint, action);
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
	}
}