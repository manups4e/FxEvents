using FxEvents.Shared;
using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.Encryption;
using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.Snowflakes;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
    public class ClientGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }

        private EventDispatcher _eventDispatcher => EventDispatcher.Instance;
        private Curve25519 _curve25519;
        private byte[] _secret = [];


        public ClientGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(1, 199));
            _curve25519 = Curve25519.Create();
            Serialization = new MsgPackSerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PrepareDelegate = PrepareAsync;
            PushDelegate = Push;
            PushDelegateLatent = PushLatent;
        }

        internal void AddEvents()
        {
            _eventDispatcher.AddEventHandler(InboundPipeline, new Action<byte[]>(async encrypted =>
            {
                try
                {
                    await ProcessInboundAsync(new ServerId().Handle, encrypted);
                }
                catch (Exception ex)
                {
                    EventMessage message = encrypted.DecryptObject<EventMessage>("PLACEHOLDER");
                    Logger.Error($"InboundPipeline [{message.Endpoint}]:" + ex.ToString());
                }
            }));

            _eventDispatcher.AddEventHandler(OutboundPipeline, new Action<byte[]>(serialized =>
            {
                try
                {
                    ProcessOutbound(serialized);
                }
                catch (Exception ex)
                {
                    Logger.Error("OutboundPipeline:" + ex.ToString());
                }
            }));

            _eventDispatcher.AddEventHandler(SignaturePipeline, new Action<byte[]>(signature => {
                Logger.Debug($"Signature {signature} received from server");
                _secret = _curve25519.GetSharedSecret(signature);
            }));
            BaseScript.TriggerServerEvent(SignaturePipeline, _curve25519.GetPublicKey());
        }

        public async Task PrepareAsync(string pipeline, int source, IMessage message)
        {
            if (_secret.Length == 0)
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                while (_secret.Length == 0) await BaseScript.Delay(0);
                if (EventDispatcher.Debug)
                {
                    Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
                }
            }

            message.Signature = _secret;
        }

        public void Push(string pipeline, int source, byte[] buffer)
        {
            if (source != -1) throw new Exception($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
            BaseScript.TriggerServerEvent(pipeline, buffer);
        }

        public void PushLatent(string pipeline, int source, int bytePerSecond, byte[] buffer)
        {
            if (source != -1) throw new Exception($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
            BaseScript.TriggerLatentServerEvent(pipeline, bytePerSecond, buffer);
        }

        public async void Send(string endpoint, params object[] args)
        {
            await SendInternal(EventFlowType.Straight, new ServerId().Handle, endpoint, args);
        }
        public async void SendLatent(string endpoint, int bytePerSecond, params object[] args)
        {
            await SendInternalLatent(EventFlowType.Straight, new ServerId().Handle, endpoint, bytePerSecond, args);
        }

        public async Task<T> Get<T>(string endpoint, params object[] args)
        {
            return await GetInternal<T>(new ServerId().Handle, endpoint, args);
        }
    }
}