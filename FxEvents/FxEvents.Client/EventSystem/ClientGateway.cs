using FxEvents.Shared.Diagnostics;
using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Serialization;
using FxEvents.Shared.Serialization.Implementations;
using FxEvents.Shared.Snowflakes;
using System;
using System.Threading.Tasks;

namespace FxEvents.EventSystem
{
    public class ClientGateway : BaseGateway
    {
        protected override ISerialization Serialization { get; }
        private string _signature;

        public ClientGateway()
        {
            SnowflakeGenerator.Create((short)new Random().Next(1, 199));
            Serialization = new MsgPackSerialization();
            DelayDelegate = async delay => await BaseScript.Delay(delay);
            PrepareDelegate = PrepareAsync;
            PushDelegate = Push;
        }

        internal void AddEvents()
        {
            EventDispatcher.Instance.AddEventHandler(InboundPipeline, Func.Create<Remote, byte[]>(OnInboundPipelineHandler));

            EventDispatcher.Instance.AddEventHandler(OutboundPipeline, Func.Create<byte[]>(serialized =>
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

            EventDispatcher.Instance.AddEventHandler(SignaturePipeline, Func.Create<string>(signature => _signature = signature));
            Events.TriggerServerEvent(SignaturePipeline);
        }

        private async void OnInboundPipelineHandler([Source] Remote remote, byte[] serialized)
        {
            try
            {
                await ProcessInboundAsync(remote, serialized);
            }
            catch (Exception ex)
            {
                Logger.Error("InboundPipeline:" + ex.ToString());
            }
        }

        public async Coroutine PrepareAsync(string pipeline, int source, IMessage message)
        {
            if (string.IsNullOrWhiteSpace(_signature))
            {
                StopwatchUtil stopwatch = StopwatchUtil.StartNew();
                while (_signature == null) await BaseScript.Yield();
                if (EventDispatcher.Debug)
                {
                    Logger.Debug($"[{message}] Halted {stopwatch.Elapsed.TotalMilliseconds}ms due to signature retrieval.");
                }
            }

            message.Signature = _signature;
        }

        public void Push(string pipeline, int source, byte[] buffer)
        {
            if (source != -1) throw new Exception($"The client can only target server events. (arg {nameof(source)} is not matching -1)");
            Events.TriggerServerEvent(pipeline, buffer);
        }

        public async void Send(string endpoint, params object[] args)
        {
            await SendInternal(EventFlowType.Straight, new ServerId().Handle, endpoint, args);
        }

        public async Task<T> Get<T>(string endpoint, params object[] args)
        {
            return await GetInternal<T>(new ServerId().Handle, endpoint, args);
        }
    }
}