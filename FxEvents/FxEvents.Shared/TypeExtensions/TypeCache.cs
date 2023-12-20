using System;
/// <summary>
///     A simple type cache to alleviate the reflection lookups. Checking for simple types is done once per type
///     encountered. Rather than every time. (Saves many CPU cycles. Reflection is slow)
/// </summary>
/// <typeparam name="T"></typeparam>

namespace FxEvents.Shared.TypeExtensions
{
    public static class TypeCache<T>
    {
        static TypeCache()
        {
            Type = typeof(T);
            IsSimpleType = true;
            switch (Type.GetTypeCode(Type))
            {
                case TypeCode.Object:
                case TypeCode.DBNull:
                case TypeCode.Empty:
                case TypeCode.DateTime:
                    IsSimpleType = false;
                    break;
            }
        }

        // ReSharper disable StaticMemberInGenericType
        public static bool IsSimpleType { get; }
        public static Type Type { get; }
        // ReSharper restore StaticMemberInGenericType
    }

    public static class TypeCache
    {
        public static bool IsSimpleType(this Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.Object or TypeCode.DBNull or TypeCode.Empty or TypeCode.DateTime => false,
            _ => true,
        };
    }
}