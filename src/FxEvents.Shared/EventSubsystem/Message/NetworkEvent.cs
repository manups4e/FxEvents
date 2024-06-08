using FxEvents.Shared.Message;
using FxEvents.Shared.Snowflakes;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Message
{
    internal struct NetworkEvent
    {
        public string Sender { get; }
        public Snowflake Id { get; }
        public EventFlowType FlowType { get; }
        public string Endpoint { get; }
        public object Payload { get; }
        public long Timestamp { get; }
        public bool HasResponse { get; set; }
        public object? Response { get; set; }
        public long? ResponseTimestamp { get; set; }
        public NetworkEvent(string sender, Snowflake id, EventFlowType flowType, string endpoint, object payload, long timestamp) : this()
        {
            Sender = sender;
            Id = id;
            FlowType = flowType;
            Endpoint = endpoint;
            Payload = payload;
            Timestamp = timestamp;
            HasResponse = false;
        }

        public NetworkEvent(string sender, EventMessage message) : this(sender, message.Id, message.Flow, message.Endpoint, message.Parameters, Clock.GetMilliseconds())
        {
        }

        public void SetResponseData(object? response, long timestamp)
        {
            HasResponse = true;
            Response = response;
            ResponseTimestamp = timestamp;
        }
    }
}
