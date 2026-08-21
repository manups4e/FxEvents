using MessagePack;

namespace FxEvents.Shared.Payload
{
	[MessagePackObject]
	public class EventParameter
	{
		[Key(0)]
		public byte[] Data { get; set; }

		public EventParameter() { }

		public EventParameter(byte[] data)
		{
			Data = data;
		}
	}
}