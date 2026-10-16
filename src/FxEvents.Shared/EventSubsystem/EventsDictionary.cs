
using FxEvents.Shared.Exceptions;
using FxEvents.Shared.TypeExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace FxEvents.Shared.EventSubsystem
{
    public enum SourceParamKind
    {
        None,
        Player,
        String,
        Int,
        ISource
    }

    public struct HandlerParamMetadata
    {
        public Type ParameterType;
        public bool IsSimpleType;
        public object? DefaultValue;
    }

    public class EventCallback : Tuple<Delegate, Binding>
    {
        public Delegate Callback => Item1;
        public Binding Binding => Item2;
        public bool TakesSource { get; }
        public SourceParamKind SourceKind { get; }
        public Func<int, ISource>? SourceActivator { get; }
        public HandlerParamMetadata[] Parameters { get; }
        public int? CustomTimeoutMs { get; }

        public EventCallback(Delegate @delegate, Binding binding) : base(@delegate, binding)
        {
            MethodInfo method = @delegate.Method;
            ParameterInfo[] allParams = method.GetParameters();

            EventTimeoutAttribute timeoutAttr = method.GetCustomAttribute<EventTimeoutAttribute>();
            CustomTimeoutMs = timeoutAttr?.TimeoutMs;

#if SERVER
            var fromSourceParam = allParams.FirstOrDefault(self => self.GetCustomAttribute<FromSourceAttribute>() != null);
            if (fromSourceParam != null)
            {
                TakesSource = true;
                Type pType = fromSourceParam.ParameterType;
                if (typeof(Player).IsAssignableFrom(pType))
                {
                    SourceKind = SourceParamKind.Player;
                }
                else if (typeof(ISource).IsAssignableFrom(pType))
                {
                    SourceKind = SourceParamKind.ISource;
                    ConstructorInfo constructor = pType.GetConstructors().FirstOrDefault(x => x.GetParameters().Any(y => y.ParameterType == typeof(int)))
                        ?? throw new Exception($"No constructor found with int handle to initialize ISource class '{pType.FullName}'.");

                    ParameterExpression parameter = Expression.Parameter(typeof(int), "handle");
                    NewExpression expression = Expression.New(constructor, parameter);
                    SourceActivator = Expression.Lambda<Func<int, ISource>>(expression, parameter).Compile();
                }
                else if (typeof(string).IsAssignableFrom(pType))
                {
                    SourceKind = SourceParamKind.String;
                }
                else if (typeof(int).IsAssignableFrom(pType))
                {
                    SourceKind = SourceParamKind.Int;
                }
                else
                {
                    SourceKind = SourceParamKind.None;
                }
            }
#endif
            int startingIndex = TakesSource ? 1 : 0;
            int payloadParamCount = allParams.Length - startingIndex;
            Parameters = new HandlerParamMetadata[payloadParamCount];

            for (int i = 0; i < payloadParamCount; i++)
            {
                ParameterInfo pInfo = allParams[startingIndex + i];
                Type pType = pInfo.ParameterType;
                Parameters[i] = new HandlerParamMetadata
                {
                    ParameterType = pType,
                    IsSimpleType = TypeCache.IsSimpleType(pType),
                    DefaultValue = pInfo.DefaultValue ?? (pType.IsValueType ? Activator.CreateInstance(pType) : null)
                };
            }
        }
    }

    public class EventsDictionary : ConcurrentDictionary<string, EventEntry>
    {
        public EventsDictionary() : base(StringComparer.OrdinalIgnoreCase) { }

        public new EventEntry this[string key]
        {
            get => GetOrAdd(key, k => new EventEntry(k));
            set => base[key] = value;
        }

        public void Add(string endpoint, Binding binding, Delegate callback)
        {
            this[endpoint] += new EventCallback(callback, binding);
        }

        public bool Remove(string endpoint)
        {
            return TryRemove(endpoint, out _);
        }
    }

    public class EventEntry
    {
        internal readonly string m_eventName;
        internal readonly List<EventCallback> m_callbacks = new();
        internal string name => m_eventName;

        public EventEntry(string eventName)
        {
            m_eventName = eventName;
        }

        public static EventEntry operator +(EventEntry entry, Tuple<Delegate, Binding> deleg)
        {
            entry.m_callbacks.Add(deleg as EventCallback ?? new EventCallback(deleg.Item1, deleg.Item2));
            return entry;
        }

        public static EventEntry operator -(EventEntry entry, Tuple<Delegate, Binding> deleg)
        {
            for (int i = entry.m_callbacks.Count - 1; i >= 0; i--)
            {
                if (entry.m_callbacks[i].Item1 == deleg.Item1 && entry.m_callbacks[i].Item2 == deleg.Item2)
                {
                    entry.m_callbacks.RemoveAt(i);
                    break;
                }
            }
            return entry;
        }
    }
}
