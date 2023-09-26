global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
using FxEvents.Shared.EventSubsystem;
using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FxEvents
{
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger { get; set; }
        internal static EventDispatcher Instance { get; set; }
        internal ExportDictionary GetExports => Exports;
        internal PlayerList GetPlayers => Players;
        internal static ServerGateway Events { get; set; }
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;

        public EventDispatcher()
        {
            Logger = new Log();
            Instance = this;
            string debugMode = API.GetResourceMetadata(API.GetCurrentResourceName(), "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || int.TryParse(debugMode, out int num) && num > 0;
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
            Events = new ServerGateway();
            Events.SignaturePipeline = _sig;
            Events.InboundPipeline = _in;
            Events.OutboundPipeline = _out;
            Initialized = true;
            Events.AddEvents();
        }

        /// <summary>
        /// registra un evento (TriggerEvent)
        /// </summary>
        /// <param name="name">Nome evento</param>
        /// <param name="action">Azione legata all'evento</param>
        internal async void AddEventHandler(string eventName, Delegate action)
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
            Events.Send(Convert.ToInt32(player.Handle), endpoint, args);
        }
        public static void Send(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Send(client.Handle, endpoint, args);
        }
        public static void Send(IEnumerable<Player> players, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Send(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        }
        public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Send(clients.Select(x => x.Handle).ToList(), endpoint, args);
        }
        public static Task<T> Get<T>(Player player, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return Events.Get<T>(Convert.ToInt32(player.Handle), endpoint, args);
        }
        public static Task<T> Get<T>(ISource client, string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return Events.Get<T>(client.Handle, endpoint, args);
        }
        public static void Mount(string endpoint, Delegate @delegate)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Mount(endpoint, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Unmount(endpoint);
        }
    }
}