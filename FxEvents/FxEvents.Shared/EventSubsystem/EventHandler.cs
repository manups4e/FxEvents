using FxEvents.Shared.Snowflakes;
using System;

namespace FxEvents.Shared.EventSubsystem
{
    public class EventHandler
    {
        public Snowflake Id { get; set; }
        public string Endpoint { get; set; }
        public Delegate Delegate { get; set; }

        public EventHandler(string endpoint, Delegate @delegate)
        {
            Id = Snowflake.Next();
            Endpoint = endpoint;
            Delegate = @delegate;
        }
    }
}