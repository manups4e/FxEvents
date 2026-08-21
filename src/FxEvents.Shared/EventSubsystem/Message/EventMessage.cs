using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Message;
using FxEvents.Shared.Payload;
using FxEvents.Shared.Snowflakes;
using MessagePack;
using System.Collections.Generic;

[MessagePackObject(AllowPrivate = true)]
internal class EventMessage : IMessage
{
	[Key(0)]
	public Snowflake Id { get; set; }

	[Key(1)]
	public string? Endpoint { get; set; }

	[Key(2)]
	public EventFlowType Flow { get; set; }

	[Key(3)]
	public EventRemote Sender { get; set; }

	[Key(4)]
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