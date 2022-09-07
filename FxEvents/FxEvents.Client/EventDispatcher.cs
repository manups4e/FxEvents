global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared.EventSubsystem;
using Logger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FxEvents
{
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger;
        internal static EventDispatcher Instance { get; set; }
        internal static ClientGateway Events;
        internal PlayerList GetPlayers => Players;
        internal static bool Debug { get; set; }
        public EventDispatcher() 
        {
            Logger = new();
            Instance = this;
            Events = new();
            string debugMode = API.GetResourceMetadata(API.GetCurrentResourceName(), "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || Convert.ToInt32(debugMode) > 0;
        }

        /// <summary>
        /// registra un evento client (TriggerEvent)
        /// </summary>
        /// <param name="eventName">Nome evento</param>
        /// <param name="action">Azione legata all'evento</param>
        internal void AddEventHandler(string eventName, Delegate action)
        {
            EventHandlers[eventName] += action;
        }

        public static void Send(string endpoint, params object[] args) => Events.Send(endpoint, args);
        public static async Task<T> Get<T>(string endpoint, params object[] args) => await Events.Get<T>(endpoint, args);
        public static void Mount(string endpoint, Delegate @delegate) => Events.Mount(endpoint, @delegate);
        public static void Unmount(string endpoint) => Events.Unmount(endpoint);

    }
}