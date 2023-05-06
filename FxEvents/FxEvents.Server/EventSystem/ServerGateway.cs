using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.TypeExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace FxEvents.EventSystem
{
    public class ServerGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }
        private Dictionary<int, string> _signatures;

        public ServerGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(200, 399));
            Serialization = new MsgPackSerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PushDelegate = Push;
            _signatures = new();
        }

        internal void AddEvents()
        {
            EventDispatcher.Instance.AddEventHandler(SignaturePipeline, new Action<Remote>(GetSignature));
            EventDispatcher.Instance.AddEventHandler(InboundPipeline, new Action<Remote, byte[]>(Inbound));
            EventDispatcher.Instance.AddEventHandler(OutboundPipeline, new Action<Remote, byte[]>(Outbound));
        }

        public void Push(string pipeline, int source, byte[] buffer)
        {
            if (source != new ServerId().Handle)
                Events.TriggerClientEvent(pipeline, EventDispatcher.Instance.GetPlayers[source], buffer);
            else
                Events.TriggerAllClientsEvent(pipeline, buffer);
        }


        private void GetSignature(Remote source)
        {
            try
            {
                int client = int.Parse(source.ToString().Substring(7, source.ToString().Length - 1));

                if (_signatures.ContainsKey(client))
                {
                    Logger.Warning($"Client {(string)Natives.GetPlayerName("" + client)}[{client}] tried acquiring event signature more than once.");
                    return;
                }

                byte[] holder = new byte[128];

                using (RNGCryptoServiceProvider service = new RNGCryptoServiceProvider())
                {
                    service.GetBytes(holder);
                }

                string signature = BitConverter.ToString(holder).Replace("-", "").ToLower();

                _signatures.Add(client, signature);
                Events.TriggerClientEvent(SignaturePipeline, EventDispatcher.Instance.GetPlayers[client], signature);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async void Inbound(Remote source, byte[] buffer)
        {
            try
            {
                int client = int.Parse(source.ToString().Substring(7, source.ToString().Length - 1));

                if (!_signatures.TryGetValue(client, out string signature)) return;

                using SerializationContext context = new SerializationContext(InboundPipeline, null, Serialization, buffer);

                EventMessage message = context.Deserialize<EventMessage>();


                if (!VerifySignature(client, message, signature)) return;

                try
                {
                    await ProcessInboundAsync(message, client);
                }
                catch (TimeoutException)
                {
                    Natives.DropPlayer(client.ToString(), $"Operation timed out: {message.Endpoint.ToBase64()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public bool VerifySignature(int source, IMessage message, string signature)
        {
            if (message.Signature == signature) return true;

            Logger.Error($"[{message.Endpoint}] Client {source} had invalid event signature, aborting:");
            Logger.Error($"[{message.Endpoint}] \tSupplied Signature: {message.Signature}");
            Logger.Error($"[{message.Endpoint}] \tActual Signature: {signature}");

            return false;
        }

        private void Outbound(Remote source, byte[] buffer)
        {
            try
            {
                int client = int.Parse(source.ToString().Substring(7, source.ToString().Length - 1));

                if (!_signatures.TryGetValue(client, out string signature)) return;

                using SerializationContext context = new SerializationContext(OutboundPipeline, null, Serialization, buffer);

                EventResponseMessage response = context.Deserialize<EventResponseMessage>();

                if (!VerifySignature(client, response, signature)) return;

                ProcessOutbound(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public void Send(Player player, string endpoint, params object[] args) => Send(Convert.ToInt32(player.Handle), endpoint, args);
        public void Send(ISource client, string endpoint, params object[] args) => Send(client.Handle, endpoint, args);
        public void Send(List<Player> players, string endpoint, params object[] args) => Send(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        public void Send(List<ISource> clients, string endpoint, params object[] args) => Send(clients.Select(x => x.Handle).ToList(), endpoint, args);

        public async void Send(List<int> targets, string endpoint, params object[] args)
        {
            int i = 0;
            while (i < targets.Count)
            {
                await BaseScript.Delay(0);
                Send(targets[i], endpoint, args);
                i++;
            }
        }

        public async void Send(int target, string endpoint, params object[] args)
        {
            await SendInternal(EventFlowType.Straight, target, endpoint, args);
        }

        public Coroutine<T> Get<T>(Player player, string endpoint, params object[] args) =>
            Get<T>(Convert.ToInt32(player.Handle), endpoint, args);

        public Coroutine<T> Get<T>(ISource client, string endpoint, params object[] args) =>
            Get<T>(client.Handle, endpoint, args);

        public async Coroutine<T> Get<T>(int target, string endpoint, params object[] args)
        {
            return await GetInternal<T>(target, endpoint, args);
        }
    }
}