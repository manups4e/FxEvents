using System;

[AttributeUsage(AttributeTargets.Method)]
public class EventTimeoutAttribute : Attribute
{
	public int TimeoutMs { get; }
	public EventTimeoutAttribute(int timeoutMs)
	{
		TimeoutMs = timeoutMs;
	}
}