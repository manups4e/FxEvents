using System;
using System.Collections.Generic;
using System.Linq;

namespace FxEvents.SourceGenerator
{
    internal sealed class EventMethodModel : IEquatable<EventMethodModel>
    {
        public string ContainingTypeFullyQualified { get; }
        public string MethodName { get; }
        public string EventName { get; }
        public int BindingValue { get; }
        public string[] ParameterTypes { get; }
        public string? ReturnType { get; }
        public bool ReturnsVoid { get; }

        public EventMethodModel(
            string containingTypeFullyQualified,
            string methodName,
            string eventName,
            int bindingValue,
            string[] parameterTypes,
            string? returnType,
            bool returnsVoid)
        {
            ContainingTypeFullyQualified = containingTypeFullyQualified;
            MethodName = methodName;
            EventName = eventName;
            BindingValue = bindingValue;
            ParameterTypes = parameterTypes ?? Array.Empty<string>();
            ReturnType = returnType;
            ReturnsVoid = returnsVoid;
        }

        public bool Equals(EventMethodModel? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ContainingTypeFullyQualified == other.ContainingTypeFullyQualified
                && MethodName == other.MethodName
                && EventName == other.EventName
                && BindingValue == other.BindingValue
                && ReturnsVoid == other.ReturnsVoid
                && ReturnType == other.ReturnType
                && ParameterTypes.SequenceEqual(other.ParameterTypes);
        }

        public override bool Equals(object? obj) => Equals(obj as EventMethodModel);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (ContainingTypeFullyQualified?.GetHashCode() ?? 0);
                hash = hash * 31 + (MethodName?.GetHashCode() ?? 0);
                hash = hash * 31 + (EventName?.GetHashCode() ?? 0);
                hash = hash * 31 + BindingValue.GetHashCode();
                hash = hash * 31 + ReturnsVoid.GetHashCode();
                hash = hash * 31 + (ReturnType?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
