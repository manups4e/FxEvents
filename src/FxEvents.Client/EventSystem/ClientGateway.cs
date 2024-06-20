﻿using FxEvents.Shared;
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

        private EventHub _hub => EventHub.Instance;
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
            _hub.AddEventHandler(InboundPipeline, new Action<string, byte[]>(async (endpoint, encrypted) =>
            {
                try
                {
                    await ProcessInboundAsync(new ServerId().Handle, endpoint, encrypted);
                }
                catch (Exception ex)
                {
                    EventMessage message = encrypted.DecryptObject<EventMessage>();
                    Logger.Error($"InboundPipeline [{message.Endpoint}]:" + ex.ToString());
                }
            }));

            _hub.AddEventHandler(OutboundPipeline, new Action<string, byte[]>((endpoint, serialized) =>
            {
                try
                {
                    ProcessReply(serialized);
                }
                catch (Exception ex)
                {
                    Logger.Error("OutboundPipeline:" + ex.ToString());
                }
            }));

            _hub.AddEventHandler(SignaturePipeline, new Action<byte[]>(signature => _secret = _curve25519.GetSharedSecret(signature)));
            BaseScript.TriggerServerEvent(SignaturePipeline, _curve25519.GetPublicKey());
        }

        internal async Task PrepareAsync(string pipeline, int source, IMessage message)
        {
            if (_secret.Length == 0)
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                while (_secret.Length == 0) await BaseScript.Delay(0);
                if (EventHub.Debug)
                {
                    Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
                }
            }
        }

        internal void Push(string pipeline, int source, string endpoint, byte[] buffer)
        {
            if (source != -1) throw new Exception($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
            BaseScript.TriggerServerEvent(pipeline, endpoint, buffer);
        }

        internal void PushLatent(string pipeline, int source, int bytePerSecond, string endpoint, byte[] buffer)
        {
            if (source != -1) throw new Exception($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
            BaseScript.TriggerLatentServerEvent(pipeline, bytePerSecond, endpoint, buffer);
        }

        public async void Send(string endpoint, params object[] args)
        {
            await CreateAndSendAsync(EventFlowType.Straight, new ServerId().Handle, endpoint, args);
        }
        public async void SendLatent(string endpoint, int bytePerSecond, params object[] args)
        {
            await CreateAndSendLatentAsync(EventFlowType.Straight, new ServerId().Handle, endpoint, bytePerSecond, args);
        }

        public async Task<T> Get<T>(string endpoint, params object[] args)
        {
            return await GetInternal<T>(new ServerId().Handle, endpoint, args);
        }

        internal byte[] GetSecret(int _)
        {
            if (_secret == null)
                throw new Exception("Shared Encryption Secret has not been generated yet");
            return _secret;
        }
    }
}