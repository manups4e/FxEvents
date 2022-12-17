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


        public EventDispatcher()
        {
            Instance = this;
            Logger = new Log();
            Events = new ServerGateway();
            string debugMode = API.GetResourceMetadata(API.GetCurrentResourceName(), "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || Convert.ToInt32(debugMode) > 0;
        }

        /// <summary>
        /// registra un evento (TriggerEvent)
        /// </summary>
        /// <param name="name">Nome evento</param>
        /// <param name="action">Azione legata all'evento</param>
        internal void AddEventHandler(string eventName, Delegate action)
        {
            EventHandlers[eventName] += action;
        }

        public static void Send(Player player, string endpoint, params object[] args) => Events.Send(Convert.ToInt32(player.Handle), endpoint, args);
        public static void Send(ISource client, string endpoint, params object[] args) => Events.Send(client.Handle, endpoint, args);
        public static void Send(IEnumerable<Player> players, string endpoint, params object[] args) => Events.Send(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args) => Events.Send(clients.Select(x => x.Handle).ToList(), endpoint, args);
        public static Task<T> Get<T>(Player player, string endpoint, params object[] args) => Events.Get<T>(Convert.ToInt32(player.Handle), endpoint, args);
        public static Task<T> Get<T>(ISource client, string endpoint, params object[] args) => Events.Get<T>(client.Handle, endpoint, args);
        public static void Mount(string endpoint, Delegate @delegate) => Events.Mount(endpoint, @delegate);
        public static void Unmount(string endpoint) => Events.Unmount(endpoint);
    }
}