using FxEvents.Shared.TypeExtensions;
using MsgPack;
using System;
using System.Globalization;

internal static class BaseGatewayHelpers
{

    internal static object GetHolder(MessagePackObject msgpkObj, Type type)
    {
        object obj = msgpkObj.ToObject();
        TypeCode typeCode = Type.GetTypeCode(type);
        switch (typeCode)
        {
            case TypeCode.String:
                if (msgpkObj.IsNil)
                    return string.Empty;
                return obj as string ?? (type.IsSimpleType() ? obj.ToString() : string.Empty);
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                if (obj is IConvertible convertible)
                {
                    try
                    {
                        return Convert.ChangeType(convertible, type);
                    }
                    catch (InvalidCastException)
                    {
                        return GetDefaultForType(type);
                    }
                }
                else
                    return GetDefaultForType(type);
            case TypeCode.Boolean:
                bool booleanValue;
                if (bool.TryParse(obj.ToString(), out booleanValue))
                    return booleanValue;
                else
                    return false;
            case TypeCode.Char:
                char charValue;
                if (char.TryParse(obj.ToString(), out charValue))
                    return charValue;
                else
                    return '\0';
            case TypeCode.Decimal:
                decimal decimalValue;
                if (decimal.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue))
                    return decimalValue;
                else
                    return 0M;
            case TypeCode.Single:
                float floatValue;
                if (float.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out floatValue))
                    return floatValue;
                else
                    return 0F;
            case TypeCode.Double:
                double doubleValue;
                if (double.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                    return doubleValue;
                else
                    return 0D;
            case TypeCode.DBNull:
            case TypeCode.DateTime:
                return obj;
            default:
                return GetDefaultForType(type);
        }
    }
    private static object GetDefaultForType(Type type)
    {
        // Determine the default value for the given type
        if (type == typeof(string)) return string.Empty;
        if (type == typeof(byte)) return 0;
        if (type == typeof(sbyte)) return 0;
        if (type == typeof(bool)) return false;
        if (type == typeof(char)) return '\0';
        if (type == typeof(DateTime)) return DateTime.MinValue;
        if (type == typeof(decimal)) return 0M;
        if (type == typeof(float)) return 0F;
        if (type == typeof(double)) return 0D;
        if (type == typeof(short)) return 0;
        if (type == typeof(int)) return 0;
        if (type == typeof(long)) return 0L;
        if (type == typeof(ushort)) return 0U;
        if (type == typeof(uint)) return 0U;
        if (type == typeof(ulong)) return 0UL;
        return null; // Fallback to null for unsupported types
    }
}