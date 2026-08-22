global using CitizenFX.FiveM.Client;
global using CitizenFX.FiveM.Shared.Script;
global using static CitizenFX.FiveM.Client.Native;
using CitizenFX.FiveM.Client.Entities;
using CitizenFX.FiveM.Shared;
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
		internal static ClientGateway Gateway { get; set; }
		internal static bool Debug { get; set; }
		public static bool Initialized { get; private set; } = false;
		internal static EventHub Instance { get; private set; }

		public static EventsDictionary Events => Gateway._handlers;

		/// <summary>
		/// Inizializza l'EventHub lato Client.
		/// </summary>
		public static void Initialize()
		{
			if (Initialized) return;

			// Imposta subito la flag per spezzare qualsiasi loop di ricorsione
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

		internal void AddEventHandler(string eventName, Delegate action)
		{
			SharedAPI.OnNetEvent(eventName, action);
		}

		#region Public Static API
		public static void Send(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(endpoint, Binding.Remote, args);
		}

		public static void SendLocal(string endpoint, params object[] args)
		{
			EnsureInitialized();
			Gateway.Send(endpoint, Binding.Local, args);
		}

		public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
		{
			EnsureInitialized();
			Gateway.SendLatent(endpoint, bytesPerSeconds, args);
		}

		public static async Task<T?> Get<T>(string endpoint, params object[] args)
		{
			EnsureInitialized();
			return await Gateway.Get<T>(endpoint, args);
		}

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
		#endregion
	}
}