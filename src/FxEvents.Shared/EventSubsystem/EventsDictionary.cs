using System;
using System.Collections.Generic;

namespace FxEvents.Shared.EventSubsystem
{
	public readonly record struct EventCallback(Delegate Callback, bool IsRemote);

	public class EventsDictionary : Dictionary<string, EventEntry>
	{
		public new EventEntry this[string key]
		{
			get
			{
				var lookupKey = key.ToLowerInvariant();

				if (TryGetValue(lookupKey, out var entry))
				{
					return entry;
				}

				entry = new EventEntry(key);
				base.Add(lookupKey, entry);
				return entry;
			}
			set { }
		}

		public void Add(string endpoint, bool isRemote, Delegate callback)
		{
			this[endpoint] += new EventCallback(callback, isRemote);
		}
	}

	public class EventEntry
	{
		internal readonly string m_eventName;
		internal readonly List<EventCallback> m_callbacks = [];

		public string Name => m_eventName;

		public EventEntry(string eventName)
		{
			m_eventName = eventName;
		}

		public static EventEntry operator +(EventEntry entry, EventCallback callback)
		{
			entry.m_callbacks.Add(callback);
			return entry;
		}

		public static EventEntry operator -(EventEntry entry, EventCallback callback)
		{
			entry.m_callbacks.Remove(callback);
			return entry;
		}
	}
}