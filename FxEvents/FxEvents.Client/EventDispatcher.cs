global using CitizenFX.Core;
using CitizenFX.FiveM.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
using Logger;
using System;

namespace FxEvents
{
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger;
        internal static EventDispatcher Instance { get; set; }
        internal static ClientGateway Events;
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;

        public EventDispatcher()
        {
            Logger = new Log();
            Instance = this;
            string debugMode = Natives.GetResourceMetadata(Natives.GetCurrentResourceName(), "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || Convert.ToInt32(debugMode) > 0;
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
            Events = new ClientGateway();
            Events.SignaturePipeline = _sig;
            Events.InboundPipeline = _in;
            Events.OutboundPipeline = _out;
            Initialized = true;
            Events.AddEvents();
        }

        /// <summary>
        /// Register an Event (TriggerEvent)
        /// </summary>
        /// <param name="name">Event Name</param>
        /// <param name="action">Event-related action</param>
        internal async void AddEventHandler(string eventName, Delegate action)
        {
            while (!Initialized) await BaseScript.Delay(0);
            EventHandlers[eventName].Add(Func.Create(action), Binding.All);
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
        public static async Coroutine<T> Get<T>(string endpoint, params object[] args)
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