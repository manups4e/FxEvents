global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents.EventSystem;
using FxEvents.Shared;
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
    [Obsolete("Use EventHub instead, this class will be removed soon")]
    public class EventDispatcher : BaseScript
    {
        internal static Log Logger;
        internal PlayerList GetPlayers => EventHub.Instance.GetPlayers;
        internal static ClientGateway Gateway => EventHub.Gateway;
        internal static bool Debug => EventHub.Debug;
        internal static bool Initialized => EventHub.Initialized;
        public static EventsDictionary Events => Gateway._handlers;

        public static void Initalize(string inboundEvent, string outboundEvent, string signatureEvent)
        {
            EventHub.Initialize();
        }

        internal async void AddEventHandler(string eventName, Delegate action)
        {
            EventHub.Instance.AddEventHandler(eventName, action);
        }

        public static void Send(string endpoint, params object[] args)
        {
            EventHub.Send(endpoint, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            EventHub.SendLatent(endpoint, bytesPerSeconds, args);
        }

        public static async Task<T> Get<T>(string endpoint, params object[] args)
        {
            return await EventHub.Get<T>(endpoint, args);
        }
        public static void Mount(string endpoint, Delegate @delegate)
        {
            EventHub.Mount(endpoint, Binding.Remote, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            EventHub.Unmount(endpoint);
        }

    }
}