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
        internal static Log Logger;
        internal PlayerList GetPlayers => Players;
        internal static ClientGateway clientGateway;
        internal static bool Debug { get; set; }
        internal static bool Initialized = false;
        internal static EventDispatcher Instance;
        public static EventsDictionary Events => clientGateway._handlers;

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
            clientGateway = new ClientGateway();
            clientGateway.SignaturePipeline = _sig;
            clientGateway.InboundPipeline = _in;
            clientGateway.OutboundPipeline = _out;
            Initialized = true;
            clientGateway.AddEvents();

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

                    if (method.ReturnType != null)
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
                        Mount(attribute.Name, Delegate.CreateDelegate(actionType, method));
                    else
                        Logger.Error($"Error registering method {method.Name} - FxEvents supports only Static methods for its [FxEvent] attribute!");
                }
            }
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
            clientGateway.Send(endpoint, args);
        }

        public static void SendLatent(string endpoint, int bytesPerSeconds, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            clientGateway.SendLatent(endpoint, bytesPerSeconds, args);
        }

        public static async Task<T> Get<T>(string endpoint, params object[] args)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return default;
            }
            return await clientGateway.Get<T>(endpoint, args);
        }
        public static void Mount(string endpoint, Delegate @delegate)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            clientGateway.Mount(endpoint, @delegate);
        }
        public static void Unmount(string endpoint)
        {
            if (!Initialized)
            {
                Logger.Error("Dispatcher not initialized, please initialize it and add the events strings");
                return;
            }
            clientGateway.Unmount(endpoint);
        }

    }
}