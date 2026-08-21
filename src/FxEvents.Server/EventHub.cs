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
        internal static Log Logger { get; set; }
        internal Player[] GetPlayers => API.Players.All.ToArray();
        internal static ServerGateway Gateway { get; set; }
        internal static bool Debug { get; set; }
        public static bool Initialized = false;
        internal static EventHub Instance;

        public static EventsDictionary Events => Gateway._handlers;

        public void Initialize()
        {
            Logger = new Log();
            Instance = this;
            var resName = GetCurrentResourceName();
            string debugMode = GetResourceMetadata(resName, "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || int.TryParse(debugMode, out int num) && num > 0;
            //RegisterCommand("generatekey", new Action<int, List<object>, string>(generateKeyCommand), false);
            byte[] inbound = Encryption.GenerateHash(resName + "_inbound");
            byte[] outbound = Encryption.GenerateHash(resName + "_outbound");
            byte[] signature = Encryption.GenerateHash(resName + "_signature");
			Gateway = new ServerGateway
			{
				SignaturePipeline = signature.BytesToString(),
				InboundPipeline = inbound.BytesToString(),
				OutboundPipeline = outbound.BytesToString()
			};
			SharedAPI.OnEvent("playerJoining", new Action<Player>(OnPlayerDropped));
			SharedAPI.OnEvent("playerDropped", new Action<Player>(OnPlayerDropped));
            InitializeInternal();

		}

        [OnCommand("generatekey", Restricted = true)]
        private static async void generateKeyCommand()
		{
			Logger.Info("Generating random passfrase with a 50 words dictionary...");
			Tuple<string, string> ret = await Encryption.GenerateKey();
			string print = $"Here is your generated encryption key, save it in a safe place.\nThis key is not saved by FXEvents anywhere, so please store it somewhere safe, if you save encrypted data and loose this key, your data will be lost.\n" +
			$"You can always generate new keys by using \"generatekey\" command.\n" +
			$"Passfrase: {ret.Item1}\nEncrypted Passfrase: {ret.Item2}";
			Logger.Info(print);
		}

		private void InitializeInternal()
        {
            Gateway.AddEvents();
			var assembly = Assembly.GetCallingAssembly();
            List<string> withReturnType = new List<string>();
            foreach (var type in assembly.GetTypes())
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(m => m.GetCustomAttributes(typeof(FxEventAttribute), false).Length > 0);

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    var actionType = Expression.GetDelegateType(parameters.Concat(new[] { method.ReturnType }).ToArray());
                    var attribute = method.GetCustomAttribute<FxEventAttribute>();

                    if (method.ReturnType != typeof(void))
                    {
                        if (withReturnType.Contains(attribute.Name))
                        {
                            // throw error and break execution for the script sake.
                            throw new Exception($"FxEvents - Failed registering [{attribute.Name}] delegates. Cannot register more than 1 delegate for [{attribute.Name}] with a return type!");
                        }
                        else
                        {
                            withReturnType.Add(attribute.Name);
                        }
                    }

                    if (method.IsStatic)
                    {
                        Mount(attribute.Name, attribute.Binding, Delegate.CreateDelegate(actionType, method));
                    }
                    else
                        Logger.Error($"Error registering method {method.Name} - FxEvents supports only Static methods for its [FxEvent] attribute!");
                }
            }
			Initialized = true;
		}

		/// <summary>
		/// Register an event (TriggerEvent)
		/// </summary>
		/// <param name="name">Event name</param>
		/// <param name="action">Action bound to the event</param>
		internal async void RegisterEvent(string eventName, Delegate action)
        {
            while (!Initialized) await API.Delay(0);
            SharedAPI.OnNetEvent(eventName, action);
        }

        public static void Send(Player player, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(player, endpoint, args);
        }
        public static void Send(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(client, endpoint, args);
        }
        public static void Send(IEnumerable<Player> players, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(players.ToList(), endpoint, args);
        }

        public static void Send(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }

            var playerList = Instance.GetPlayers;
            Gateway.Send(playerList.ToList(), endpoint, args);
        }

        public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(clients.ToList(), endpoint, args);
        }

        public static void SendLocal(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(endpoint, args);
        }

        public static void SendLatent(Player player, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(ISource client, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.SendLatent(client.Handle, endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<Player> players, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            var playerList = Instance.GetPlayers;
            Gateway.SendLatent(playerList.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<ISource> clients, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static async Task<T> Get<T>(Player player, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return await Gateway.Get<T>(Convert.ToInt32(player.Handle), endpoint, args);
        }

        public static async Task<T> Get<T>(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return await Gateway.Get<T>(client.Handle, endpoint, args);
        }

        public static void Mount(string endpoint, Binding binding, Delegate @delegate)
        {
            Gateway.Mount(endpoint, binding, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            Gateway.Unmount(endpoint);
        }

        private void OnPlayerDropped([FromSource] Player player)
        {
            if (Gateway._signatures.ContainsKey(player.Handle))
				Gateway._signatures.Remove(player.Handle);
        }
    }
}