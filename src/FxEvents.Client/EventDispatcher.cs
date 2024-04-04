global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
using Logger;
using System;
using System.Threading.Tasks;

namespace FxEvents
{
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger;
        internal PlayerList GetPlayers => Players;
        internal static ClientGateway Events;
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
                Logger.Fatal("FXEvents: Encryption key cannot be empty, please add an encryption key or use generatekey command in console to generate one to save");
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
            Events = new ClientGateway();
            Events.SignaturePipeline = _sig;
            Events.InboundPipeline = _in;
            Events.OutboundPipeline = _out;
            Initialized = true;
            Events.AddEvents();
        }

        /// <summary>
        /// registra un evento client (TriggerEvent)
        /// </summary>
        /// <param name="eventName">Nome evento</param>
        /// <param name="action">Azione legata all'evento</param>
        internal async void AddEventHandler(string eventName, Delegate action)
        {
            while (!Initialized) await BaseScript.Delay(0);
            EventHandlers[eventName] += action;
        }

        public static void Send(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Events.Send(endpoint, args);
        }
        public static async Task<T> Get<T>(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return await Events.Get<T>(endpoint, args);
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