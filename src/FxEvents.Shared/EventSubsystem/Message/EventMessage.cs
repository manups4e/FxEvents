using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Snowflakes;
using MessagePack;
using System.Collections.Generic;

internal class EventMessage : IMessage
{
	public Snowflake Id { get; set; }

	public string? Endpoint { get; set; }

	public EventFlowType Flow { get; set; }

	public EventRemote Sender { get; set; }

	public List<EventParameter> Parameters { get; set; }

	public EventMessage()
	{
		Parameters = new List<EventParameter>();
	}

	public EventMessage(string endpoint, EventFlowType flow, List<EventParameter> parameters, EventRemote sender)
	{
		Id = Snowflake.Next();
		Endpoint = endpoint;
		Flow = flow;
		Parameters = parameters;
		Sender = sender;
	}

	public override string ToString() => Endpoint ?? string.Empty;
}