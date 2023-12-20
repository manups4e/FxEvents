namespace FxEvents.Shared.Payload
{
    public class EventParameter
    {
        public byte[] Data { get; set; }

        public EventParameter() { }
        public EventParameter(byte[] data)
        {
            Data = data;
        }
    }
}