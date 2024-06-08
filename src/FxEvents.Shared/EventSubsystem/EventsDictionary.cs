using FxEvents.Shared.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FxEvents.Shared.EventSubsystem
{
    public class EventsDictionary : Dictionary<string, EventEntry>
    {
        public new EventEntry this[string key]
        {
            get
            {
                var lookupKey = key.ToLower();

                if (this.ContainsKey(lookupKey))
                {
                    return base[lookupKey];
                }

                var entry = new EventEntry(key);
                base.Add(lookupKey, entry);

                return entry;
            }
            set { }
        }

        public void Add(string key, Delegate value)
        {
            this[key] += value;
        }
    }

    public class EventEntry
    {
        internal readonly string m_eventName;
        internal readonly List<Delegate> m_callbacks = new List<Delegate>();
        internal string name => m_eventName;

        public EventEntry(string eventName)
        {
            m_eventName = eventName;
        }

        public static EventEntry operator +(EventEntry entry, Delegate deleg)
        {
            entry.m_callbacks.Add(deleg);

            return entry;
        }

        public static EventEntry operator -(EventEntry entry, Delegate deleg)
        {
            entry.m_callbacks.Remove(deleg);

            return entry;
        }
    }
}
