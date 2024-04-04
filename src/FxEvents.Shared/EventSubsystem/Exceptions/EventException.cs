using System;

namespace FxEvents.Shared.Exceptions
{
    public class EventTimeoutException : Exception
    {
        public EventTimeoutException(string message) : base(message)
        {
        }

        public EventTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}