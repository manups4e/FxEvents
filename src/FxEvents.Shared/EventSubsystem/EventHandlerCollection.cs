using FxEvents.Shared.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace FxEvents.Shared.EventSubsystem
{
    public class EventHandlerCollection : List<EventHandler>
    {
        public KeyValuePair<bool, int> HasSingleEndpoint(string endpoint)
        {
            IEnumerable<EventHandler> ends = find(endpoint);
            return new(ends.Count() == 1, ends.Count());
        }

        public EventHandler FindSingleEndpoint(string endpoint)
        {
            IEnumerable<EventHandler> ends = find(endpoint);
            if (ends.Count() == 0)
                throw new EventException($"Callback handler for event {endpoint} not found.");
            else if (ends.Count() > 0)
                throw new EventException($"Found multiple callback handlers for event {endpoint}, only 1 allowed.");
            return ends.ToArray()[0];
        }

        public List<EventHandler> FindAllEndpoints(string endpoint)
        {
            return find(endpoint).ToList();
        }

        private IEnumerable<EventHandler> find(string endpoint)
        {
            return this.Where(x => x.Endpoint == endpoint);
        }

        public List<EventHandler> this[string endpoint] => this.FindAllEndpoints(endpoint);
    }
}
