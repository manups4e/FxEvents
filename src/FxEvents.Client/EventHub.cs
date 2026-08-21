global using CitizenFX.FiveM.Client;
global using CitizenFX.FiveM.Shared.Script;
global using static CitizenFX.FiveM.Client.Native;
using CitizenFX.FiveM.Client.Entities;
using CitizenFX.FiveM.Shared;
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
        internal static Log Logger;
        internal Player[] GetPlayers => API.Players.All.ToArray();
        internal static ClientGateway Gateway;
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;
        internal static EventHub Instance;
        public static EventsDictionary Events => Gateway._handlers;

        public void Initialize()
        {
            Logger = new Log();
            Instance = this;
            var resName = GetCurrentResourceName();
            string debugMode = GetResourceMetadata(resName, "fxevents_debug_mode", 0);
            Debug = debugMode == "yes" || debugMode == "true" || int.TryParse(debugMode, out int num) && num > 0;

            byte[] inbound = Encryption.GenerateHash(resName + "_inbound");
            byte[] outbound = Encryption.GenerateHash(resName + "_outbound");
            byte[] signature = Encryption.GenerateHash(resName + "_signature");
			Gateway = new ClientGateway
			{
				SignaturePipeline = signature.BytesToString(),
				InboundPipeline = inbound.BytesToString(),
				OutboundPipeline = outbound.BytesToString()
			};
		}

        public static void StartEngine()
        {
            Initialized = true;
            Gateway.AddEvents();

            var assembly = Assembly.GetCallingAssembly();
            // we keep it outside because multiple classes with same event callback? no sir no.
            List<string> withReturnType = new List<string>();

            foreach (var type in assembly.GetTypes())
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.GetCustomAttributes(typeof(FxEventAttribute), false).Length > 0);

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
                        Mount(attribute.Name, attribute.Binding, Delegate.CreateDelegate(actionType, method));
                    else
                        Logger.Error($"Error registering method {method.Name} - FxEvents supports only Static methods for its [FxEvent] attribute!");
                }
            }
        }


        internal async void AddEventHandler(string eventName, Delegate action)
        {
            while (!Initialized) await API.Delay(0);
            //TODO: BOTH?
			SharedAPI.OnNetEvent(eventName, action);
			//SharedAPI.OnEvent(eventName, action);
        }

        public static void Send(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(endpoint, Binding.Remote, args);
        }

        public static void SendLocal(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Send(endpoint, Binding.Local, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.SendLatent(endpoint, bytesPerSeconds, args);
        }

        public static async Task<T> Get<T>(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return await Gateway.Get<T>(endpoint, args);
        }
        public static void Mount(string endpoint, Binding binding, Delegate @delegate)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Mount(endpoint, binding, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            Gateway.Unmount(endpoint);
        }

    }
}