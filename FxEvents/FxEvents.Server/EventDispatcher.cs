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
        internal ExportDictionary GetExports => Exports;
        internal PlayerList GetPlayers => Players;
        internal static ServerGateway Events { get; set; }
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;
        internal static string EncryptionKey = "";

        internal static EventDispatcher Instance;

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

        public static void Initalize(string inboundEvent, string outboundEvent, string signatureEvent, string encryptionKey)
        {
            if (string.IsNullOrWhiteSpace(encryptionKey))
            {
                Logger.Fatal("FXEvents: Encryption key cannot be empty, please add an encryption key or use generatekey command in console to generate one to save.");
                return;
            }
            EncryptionKey = encryptionKey;

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
        
        public static void Send(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }

            var playerList = Instance.GetPlayers;
            Events.Send(playerList.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
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