using CitizenFX.Core;
using FxEvents.Shared;
using FxEvents.Shared.Diagnostics;
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
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
    public class ServerGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }
        internal Dictionary<int, byte[]> _signatures;

        private EventHub _hub => EventHub.Instance;

        public ServerGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(200, 399));
            Serialization = new MsgPackSerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PrepareDelegate = PrepareAsync;
            PushDelegate = Push;
            PushDelegateLatent = PushLatent;
            _signatures = new();
        }

        internal void AddEvents()
        {
            _hub.RegisterEvent(SignaturePipeline, new Action<string, byte[]>(GetSignature));
            _hub.RegisterEvent(InboundPipeline, new Action<string, string, Binding, byte[]>(Inbound));
            _hub.RegisterEvent(OutboundPipeline, new Action<string, string, Binding, byte[]>(Outbound));
        }

        internal void Push(string pipeline, int source, string endpoint, Binding binding, byte[] buffer)
        {
            if (binding == Binding.All || binding == Binding.Remote)
            {
                if (source != new ServerId().Handle)
                    BaseScript.TriggerClientEvent(_hub.GetPlayers[source], pipeline, endpoint, binding, buffer);
                else
                    BaseScript.TriggerClientEvent(pipeline, endpoint, binding, buffer);
            }
            if (binding == Binding.All || binding == Binding.Local)
            {
                BaseScript.TriggerEvent(pipeline, endpoint, binding, buffer);
            }
        }

        internal void PushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer)
        {
            if (source != new ServerId().Handle)
                BaseScript.TriggerLatentClientEvent(_hub.GetPlayers[source], pipeline, bytePerSecond, endpoint, Binding.Remote, buffer);
            else
                BaseScript.TriggerLatentClientEvent(pipeline, bytePerSecond, endpoint, Binding.Remote, buffer);
        }

        private void GetSignature([FromSource] string source, byte[] clientPubKey)
        {
            try
            {
                int client = int.Parse(source.Replace("net:", string.Empty));
                if (_signatures.ContainsKey(client))
                {
                    Logger.Warning($"Client {API.GetPlayerName("" + client)}[{client}] tried acquiring event signature more than once.");
                    BaseScript.TriggerEvent("fxevents:tamperingprotection", source, "signature retrival", TamperType.REQUESTED_NEW_PUBLIC_KEY);
                    return;
                }

                Curve25519 curve25519 = Curve25519.Create();
                byte[] secret = curve25519.GetSharedSecret(clientPubKey);

                _signatures.Add(client, secret);

                BaseScript.TriggerClientEvent(_hub.GetPlayers[client], SignaturePipeline, curve25519.GetPublicKey());
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async void Inbound([FromSource] string source, string endpoint, Binding binding, byte[] encrypted)
        {
            try
            {
                int client = -1;
                if(source != null) 
                {
                    client = int.Parse(source.Replace("net:", string.Empty));

                    if (!_signatures.TryGetValue(client, out byte[] signature))
                        return;
                }

                try
                {
                    await ProcessInboundAsync(client, endpoint, binding, encrypted);
                }
                catch (TimeoutException)
                {
                    API.DropPlayer(client.ToString(), $"Operation timed out: {endpoint.ToBase64()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private void Outbound([FromSource] string source, string endpoint, Binding binding, byte[] encrypted)
        {
            try
            {
                int client = int.Parse(source.Replace("net:", string.Empty));

                if (!_signatures.TryGetValue(client, out byte[] signature)) return;

                EventResponseMessage response = encrypted.DecryptObject<EventResponseMessage>(client);

                ProcessReply(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public void Send(Player player, string endpoint, params object[] args) => Send(Convert.ToInt32(player.Handle), endpoint, Binding.Remote, args);
        public void Send(ISource client, string endpoint, params object[] args) => Send(client.Handle, endpoint, Binding.Remote, args);
        public void Send(List<Player> players, string endpoint, params object[] args) => Send(players.Select(x => int.Parse(x.Handle)).ToList(), endpoint, Binding.Remote, args);
        public void Send(List<ISource> clients, string endpoint, params object[] args) => Send(clients.Select(x => x.Handle).ToList(), endpoint, Binding.Remote, args);
        public void Send(string endpoint, params object[] args) => Send([], endpoint, Binding.Local, args);

        public async void Send(List<int> targets, string endpoint, Binding binding, params object[] args)
        {
            if (binding == Binding.Remote)
            {
                int i = 0;
                while (i < targets.Count)
                {
                    await BaseScript.Delay(0);
                    Send(targets[i], endpoint, binding, args);
                    i++;
                }
            }
            else if (binding == Binding.Local)
            {
                Send(-1, endpoint, binding, args);
            }
        }

        public async void Send(int target, string endpoint, Binding binding, params object[] args)
        {
            if (!string.IsNullOrWhiteSpace(EventHub.Instance.GetPlayers[target].Name) || (binding == Binding.Local))
                await CreateAndSendAsync(EventFlowType.Straight, target, endpoint, binding, args);
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
            if (!string.IsNullOrWhiteSpace(EventHub.Instance.GetPlayers[target].Name))
                await CreateAndSendLatentAsync(EventFlowType.Straight, target, endpoint, bytesxSecond, args);
        }

        public Task<T> Get<T>(Player player, string endpoint, params object[] args) =>
            Get<T>(Convert.ToInt32(player.Handle), endpoint, args);

        public Task<T> Get<T>(ISource client, string endpoint, params object[] args) =>
            Get<T>(client.Handle, endpoint, args);

        public async Task<T> Get<T>(int target, string endpoint, params object[] args)
        {
            return await GetInternal<T>(target, endpoint, Binding.Remote, args);
        }

        public async Task<T> GetLocal<T>(string endpoint, params object[] args)
        {
            return await GetInternal<T>(-1, endpoint, Binding.Local, args);
        }

        internal async Task PrepareAsync(string pipeline, int source, IMessage message)
        {
            if (GetSecret(source).Length == 0)
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                long time = API.GetGameTimer();
                while (GetSecret(source).Length == 0)
                {
                    if (API.GetGameTimer() - time > 1000)
                    {
                        if (EventHub.Debug)
                        {
                            Logger.Debug($"[{message}] Took to much time: {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval, client not found, still connecting or disconnected.");
                        }
                        return;
                    }
                    await BaseScript.Delay(0);
                }
                if (EventHub.Debug)
                {
                    Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
                }
            }
        }


        internal byte[] GetSecret(int source)
        {
            if (!_signatures.ContainsKey(source)) {
                Curve25519 curve25519 = Curve25519.Create();
                byte[] secret = curve25519.GetSharedSecret(curve25519.GetPublicKey());
                return secret;
            }
            return _signatures[source];
        }
    }
}