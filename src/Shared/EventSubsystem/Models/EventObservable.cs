using FxEvents.Shared.EventSubsystem;
using System;

namespace FxEvents.Shared.Models
{
    public class EventObservable
    {
        public IMessage Message { get; set; }
        public Action<byte[]> Callback { get; set; }

        public EventObservable(IMessage message, Action<byte[]> callback)
        {
            Message = message;
            Callback = callback;
        }
    }
}