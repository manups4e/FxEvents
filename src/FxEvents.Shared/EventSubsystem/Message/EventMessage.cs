using FxEvents.Shared.EventSubsystem;

using FxEvents.Shared.Payload;
using FxEvents.Shared.Snowflakes;
using System.Collections.Generic;

namespace FxEvents.Shared.Message
{
    internal class EventMessage : IMessage
    {
        public Snowflake Id { get; set; }
        public string? Endpoint { get; set; }
        public EventFlowType Flow { get; set; }
        public EventRemote Sender { get; set; }

        public IEnumerable<EventParameter> Parameters { get; set; }
        public EventMessage() { }
        public EventMessage(string endpoint, EventFlowType flow, IEnumerable<EventParameter> parameters, EventRemote sender)
        {
            Id = Snowflake.Next(); // this ensure all events have different id.. if someone tries to send an already sent event it means tampering
            Endpoint = endpoint;
            Flow = flow;
            Parameters = parameters;
            Sender = sender;
        }

        public override string ToString() => Endpoint;
    }
}