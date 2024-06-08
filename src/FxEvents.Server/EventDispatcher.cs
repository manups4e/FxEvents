global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
using FxEvents.Shared.Encryption;
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
    [Obsolete("Use EventHub instead, this class will be removed soon")]
    public class EventDispatcher
    {
        internal static Log Logger { get; set; }
        internal ExportDictionary GetExports => EventHub.Instance.GetExports;
        internal PlayerList GetPlayers => EventHub.Instance.GetPlayers;
        internal static ServerGateway Gateway => EventHub.Gateway;
        internal static bool Debug => EventHub.Debug;
        internal static bool Initialized = EventHub.Initialized;

        public static EventsDictionary Events => EventHub.Gateway._handlers;

        public static void Initalize(string inboundEvent, string outboundEvent, string signatureEvent)
        {
            EventHub.Initalize(inboundEvent, outboundEvent, signatureEvent);
        }

        internal async void RegisterEvent(string eventName, Delegate action)
        {
            EventHub.Instance.RegisterEvent(eventName, action);
        }

        public static void Send(Player player, string endpoint, params object[] args)
        {
            EventHub.Send(player, endpoint, args);
        }
        public static void Send(ISource client, string endpoint, params object[] args)
        {
            EventHub.Send(client, endpoint, args);
        }
        public static void Send(IEnumerable<Player> players, string endpoint, params object[] args)
        {
            EventHub.Send(players, endpoint, args);
        }

        public static void Send(string endpoint, params object[] args)
        {
            EventHub.Send(endpoint, args);
        }

        public static void Send(IEnumerable<ISource> clients, string endpoint, params object[] args)
        {
            EventHub.Send(clients, endpoint, args);
        }

        public static void SendLatent(Player player, string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(player, endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(ISource client, string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(client, endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<Player> players, string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(players, endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(endpoint, bytesPerSeconds, args);
        }

        public static void SendLatent(IEnumerable<ISource> clients, string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(clients, endpoint, bytesPerSeconds, args);
        }

        public static async Task<T> Get<T>(Player player, string endpoint, params object[] args)
        {
            return await EventHub.Get<T>(player, endpoint, args);
        }

        public static async Task<T> Get<T>(ISource client, string endpoint, params object[] args)
        {
            return await EventHub.Get<T>(client, endpoint, args);
        }

        public static void Mount(string endpoint, Delegate @delegate)
        {
            EventHub.Mount(endpoint, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            EventHub.Unmount(endpoint);
        }
    }
}