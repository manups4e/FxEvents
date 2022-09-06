using CitizenFX.Core;
using CitizenFX.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.TypeExtensions;
using FxEvents.Shared;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.EventSubsystem;
using System.Collections;

namespace FxEvents.EventSystem
{
    public class ServerGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }
        private Dictionary<int, string> _signatures;

        public ServerGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(200, 399));
            Serialization = new BinarySerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PushDelegate = Push;
            _signatures = new();
            EventDispatcher.Instance.AddEventHandler(EventConstant.SignaturePipeline, new Action<string>(GetSignature));
            EventDispatcher.Instance.AddEventHandler(EventConstant.InboundPipeline, new Action<string, byte[]>(Inbound));
            EventDispatcher.Instance.AddEventHandler(EventConstant.OutboundPipeline, new Action<string, byte[]>(Outbound));
        }

        public void Push(string pipeline, int source, byte[] buffer)
        {
            if (source != -1)
                BaseScript.TriggerClientEvent(EventDispatcher.Instance.GetPlayers[source], pipeline, buffer);
            else
                BaseScript.TriggerClientEvent(pipeline, buffer);
        }


        private void GetSignature([FromSource] string source)
        {
            try
            {
                var client = int.Parse(source.Replace("net:", string.Empty));

                if (_signatures.ContainsKey(client))
                {
                    Logger.Warning($"Client {API.GetPlayerName(""+client)}[{client}] tried acquiring event signature more than once.");
                    return;
                }

                var holder = new byte[128];

                using (var service = new RNGCryptoServiceProvider())
                {
                    service.GetBytes(holder);
                }

                var signature = BitConverter.ToString(holder).Replace("-", "").ToLower();

                _signatures.Add(client, signature);
                BaseScript.TriggerClientEvent(EventDispatcher.Instance.GetPlayers[client], EventConstant.SignaturePipeline, signature);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async void Inbound([FromSource] string source, byte[] buffer)
        {
            try
            {
                var client = int.Parse(source.Replace("net:", string.Empty));

                if (!_signatures.TryGetValue(client, out var signature)) return;

                using var context = new SerializationContext(EventConstant.InboundPipeline, null, Serialization, buffer);

                var message = context.Deserialize<EventMessage>();


                if (!VerifySignature(client, message, signature)) return;

                try
                {
                    await ProcessInboundAsync(message, client);
                }
                catch (TimeoutException)
                {
                    API.DropPlayer(client.ToString(), $"Operation timed out: {message.Endpoint.ToBase64()}");
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
            Logger.Error($"[{message.Endpoint}] \tActual Signature: {message.Signature}");

            return false;
        }

        private void Outbound([FromSource] string source, byte[] buffer)
        {
            try
            {
                var client = int.Parse(source.Replace("net:", string.Empty));

                if (!_signatures.TryGetValue(client, out var signature)) return;

                using var context = new SerializationContext(EventConstant.OutboundPipeline, null, Serialization, buffer);

                var response = context.Deserialize<EventResponseMessage>();

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
        public void Send(IEnumerable<Player> players, string endpoint, params object[] args) => Send(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, args);
        public void Send(IEnumerable<ISource> clients, string endpoint, params object[] args) => Send(clients.Select(x => x.Handle).ToList(), endpoint, args);

        public async void Send(List<int> targets, string endpoint, params object[] args)
        {
            var i = 0;
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

        public Task<T> Get<T>(Player player, string endpoint, params object[] args) where T : class =>
            Get<T>(Convert.ToInt32(player.Handle), endpoint, args);

        public Task<T> Get<T>(ISource client, string endpoint, params object[] args) where T : class =>
            Get<T>(client.Handle, endpoint, args);

        public async Task<T> Get<T>(int target, string endpoint, params object[] args) where T : class
        {
            return await GetInternal<T>(target, endpoint, args);
        }
    }
}