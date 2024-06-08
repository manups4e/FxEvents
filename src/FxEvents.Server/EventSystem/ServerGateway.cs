using CitizenFX.Core;
using FxEvents.Shared;
using FxEvents.Shared.Encryption;
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
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
    public class ServerGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }
        internal Dictionary<int, byte[]> _signatures;

        private EventDispatcher _eventDispatcher => EventDispatcher.Instance;

        public ServerGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(200, 399));
            Serialization = new MsgPackSerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PushDelegate = Push;
            PushDelegateLatent = PushLatent;
            _signatures = new();
        }

        internal void AddEvents()
        {
            _eventDispatcher.RegisterEvent(SignaturePipeline, new Action<string, byte[]>(GetSignature));
            _eventDispatcher.RegisterEvent(InboundPipeline, new Action<string, byte[]>(Inbound));
            _eventDispatcher.RegisterEvent(OutboundPipeline, new Action<string, byte[]>(Outbound));
        }

        public void Push(string pipeline, int source, byte[] buffer)
        {
            if (source != new ServerId().Handle)
                BaseScript.TriggerClientEvent(_eventDispatcher.GetPlayers[source], pipeline, buffer);
            else
                BaseScript.TriggerClientEvent(pipeline, buffer);
        }

        public void PushLatent(string pipeline, int source, int bytePerSecond, byte[] buffer)
        {
            if (source != new ServerId().Handle)
                BaseScript.TriggerLatentClientEvent(_eventDispatcher.GetPlayers[source], pipeline, bytePerSecond, buffer);
            else
                BaseScript.TriggerLatentClientEvent(pipeline, bytePerSecond, buffer);
        }

        private void GetSignature([FromSource] string source, byte[] clientPubKey)
        {
            try
            {
                int client = int.Parse(source.Replace("net:", string.Empty));
                if (_signatures.ContainsKey(client))
                {
                    Logger.Warning($"Client {API.GetPlayerName("" + client)}[{client}] tried acquiring event signature more than once.");
                    return;
                }

                Curve25519 curve25519 = Curve25519.Create();
                byte[] secret = curve25519.GetSharedSecret(clientPubKey);

                _signatures.Add(client, secret);
                Logger.Warning($"Client {API.GetPlayerName("" + client)}[{client}] Added signature {secret.BytesToString()}");

                BaseScript.TriggerClientEvent(_eventDispatcher.GetPlayers[client], SignaturePipeline, curve25519.GetPublicKey());
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async void Inbound([FromSource] string source, byte[] encrypted)
        {
            try
            {
                int client = int.Parse(source.Replace("net:", string.Empty));

                if (!_signatures.TryGetValue(client, out byte[] signature)) return;

                EventMessage message = encrypted.DecryptObject<EventMessage>(client);

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

        private void Outbound([FromSource] string source, byte[] encrypted)
        {
            try
            {
                int client = int.Parse(source.Replace("net:", string.Empty));

                if (!_signatures.TryGetValue(client, out byte[] signature)) return;

                EventResponseMessage response = encrypted.DecryptObject<EventResponseMessage>(client);

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

        public void SendLatent(Player player, string endpoint, int bytesxSecond, params object[] args) => SendLatent(Convert.ToInt32(player.Handle), endpoint, bytesxSecond, args);
        public void SendLatent(ISource client, string endpoint, int bytesxSecond, params object[] args) => SendLatent(client.Handle, endpoint, bytesxSecond, args);
        public void SendLatent(List<Player> players, string endpoint, int bytesxSecond, params object[] args) => SendLatent(players.Select(x => Convert.ToInt32(x.Handle)).ToList(), endpoint, bytesxSecond, args);
        public void SendLatent(List<ISource> clients, string endpoint, int bytesxSecond, params object[] args) => SendLatent(clients.Select(x => x.Handle).ToList(), endpoint, bytesxSecond, args);

        public async void SendLatent(List<int> targets, string endpoint, int bytesxSecond, params object[] args)
        {
            int i = 0;
            while (i < targets.Count)
            {
                await BaseScript.Delay(0);
                SendLatent(targets[i], endpoint, bytesxSecond, args);
                i++;
            }
        }

        public async void SendLatent(int target, string endpoint, int bytesxSecond, params object[] args)
        {
            await SendInternalLatent(EventFlowType.Straight, target, endpoint, bytesxSecond, args);
        }

        public Task<T> Get<T>(Player player, string endpoint, params object[] args) =>
            Get<T>(Convert.ToInt32(player.Handle), endpoint, args);

        public Task<T> Get<T>(ISource client, string endpoint, params object[] args) =>
            Get<T>(client.Handle, endpoint, args);

        public async Task<T> Get<T>(int target, string endpoint, params object[] args)
        {
            return await GetInternal<T>(target, endpoint, args);
        }

        internal byte[] GetSecret(int source)
        {
            if (!_signatures.ContainsKey(source))
                throw new Exception("Shared Encryption Secret has not been generated yet");
            return _signatures[source];
        }
    }
}