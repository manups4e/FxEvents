using FxEvents.Shared.Snowflakes;

namespace FxEvents.Shared.EventSubsystem
{
    public class EventHandler
    {
        public Snowflake Id { get; set; }
        public string Endpoint { get; set; }
        public DynFunc Delegate { get; set; }

        public EventHandler(string endpoint, DynFunc @delegate)
        {
            Id = Snowflake.Next();
            Endpoint = endpoint;
            Delegate = @delegate;
        }
    }
}