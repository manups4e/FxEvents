global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.EventSubsystem.Attributes;
using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace FxEvents
{
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger { get; set; }
        internal ExportDictionary GetExports => Exports;
        internal PlayerList GetPlayers => Players;
        internal static ServerGateway serverGateway { get; set; }
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;
        internal static EventDispatcher Instance;

        public static EventsDictionary Events => serverGateway._handlers;

        public EventDispatcher()
        {
            Logger = new Log();
            Instance = this;
            string debugMode = API.GetResourceMetadata(API.GetCurrentResourceName(), "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || int.TryParse(debugMode, out int num) && num > 0;
            API.RegisterCommand("generatekey", new Action<int, List<object>, string>(async (a, b, c) =>
            {
                if (a != 0) return;
                Logger.Info("Generating random passfrase with a 50 words dictionary...");
                Tuple<string, string> ret = await Encryption.GenerateKey();
                string print = $"Here is your generated encryption key, save it in a safe place.\nThis key is not saved by FXEvents anywhere, so please store it somewhere safe, if you save encrypted data and loose this key, your data will be lost.\n" +
                $"You can always generate new keys by using \"generatekey\" command.\n" +
                $"Passfrase: {ret.Item1}\nEncrypted Passfrase: {ret.Item2}";
                Logger.Info(print);
            }), false);
        }

        private static string SetSignaturePipelineString(string signatureString)
        {
            byte[] bytes = signatureString.ToBytes();
            string @event = bytes.BytesToString();
            return @event;
        }
        private static string SetInboundPipelineString(string inboundString)
        {
            byte[] bytes = inboundString.ToBytes();
            string @event = bytes.BytesToString();
            return @event;
        }
        private static string SetOutboundPipelineString(string outboundString)
        {
            byte[] bytes = outboundString.ToBytes();
            string @event = bytes.BytesToString();
            return @event;
        }

        public static void Initalize(string inboundEvent, string outboundEvent, string signatureEvent)
        {
            if (string.IsNullOrWhiteSpace(signatureEvent))
            {
                Logger.Error("SignaturePipeline cannot be null, empty or whitespace");
                return;
            }
            if (string.IsNullOrWhiteSpace(inboundEvent))
            {
                Logger.Error("InboundPipeline cannot be null, empty or whitespace");
                return;
            }
            if (string.IsNullOrWhiteSpace(outboundEvent))
            {
                Logger.Error("OutboundPipeline cannot be null, empty or whitespace");
                return;
            }
            string _sig = SetSignaturePipelineString(signatureEvent);
            string _in = SetInboundPipelineString(inboundEvent);
            string _out = SetOutboundPipelineString(outboundEvent);
            serverGateway = new ServerGateway();
            serverGateway.SignaturePipeline = _sig;
            serverGateway.InboundPipeline = _in;
            serverGateway.OutboundPipeline = _out;
            Initialized = true;
            serverGateway.AddEvents();

            var assembly = Assembly.GetCallingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.GetCustomAttributes(typeof(FxEventAttribute), false).Length > 0);

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    var actionType = Expression.GetDelegateType(parameters.Concat(new[] { typeof(void) }).ToArray());
                    var attribute = method.GetCustomAttribute<FxEventAttribute>();

                    if (method.IsStatic)
                        Mount(attribute.Name, Delegate.CreateDelegate(actionType, method));
                    else
                        Mount(attribute.Name, Delegate.CreateDelegate(actionType, Instance, method.Name));
                }
            }
        }

        /// <summary>
        /// registra un evento (TriggerEvent)
        /// </summary>
        /// <param name="name">Nome evento</param>
        /// <param name="action">Azione legata all'evento</param>
        internal async void RegisterEvent(string eventName, Delegate action)
        {
            while (!Initialized) await BaseScript.Delay(0);
            EventHandlers[eventName] += action;
        }

        public static void Send(Player player, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Send(Convert.ToInt32(player.Handle), endpoint, args);
        }
        public static void Send(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Send(client.Handle, endpoint, args);
        }
        public static void Send(IEnumerable<Player> players, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Send(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        }

        public static void Send(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }

            PlayerList playerList = Instance.GetPlayers;
            serverGateway.Send(playerList.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        }

        public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Send(clients.Select(x => x.Handle).ToList(), endpoint, args);
        }

        public static void SendLatent(Player player, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(ISource client, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.SendLatent(client.Handle, endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<Player> players, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            PlayerList playerList = Instance.GetPlayers;
            serverGateway.SendLatent(playerList.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<ISource> clients, string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesPerSeconds, args);
        }

        public static Task<T> Get<T>(Player player, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return serverGateway.Get<T>(Convert.ToInt32(player.Handle), endpoint, args);
        }

        public static Task<T> Get<T>(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return serverGateway.Get<T>(client.Handle, endpoint, args);
        }

        public static void Mount(string endpoint, Delegate @delegate)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Mount(endpoint, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            serverGateway.Unmount(endpoint);
        }
    }
}