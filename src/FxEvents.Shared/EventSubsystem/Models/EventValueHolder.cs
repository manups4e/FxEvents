namespace FxEvents.Shared.Models
{
    public class EventValueHolder<T>
    {
        public byte[] Data { get; set; }
        public T Value { get; set; }
    }
}