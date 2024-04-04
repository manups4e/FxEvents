using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Snowflakes;
using System.Collections.Generic;

namespace FxEvents.Shared.Message
{
    public class EventMessage : IMessage
    {
        public Snowflake Id { get; set; }
        public string? Signature { get; set; }
        public string? Endpoint { get; set; }
        public EventFlowType Flow { get; set; }
        public IEnumerable<EventParameter> Parameters { get; set; }
        public EventMessage() { }
        public EventMessage(string endpoint, EventFlowType flow, IEnumerable<EventParameter> parameters)
        {
            Id = Snowflake.Next();
            Endpoint = endpoint;
            Flow = flow;
            Parameters = parameters;
        }

        public override string ToString() => Endpoint;
    }
}