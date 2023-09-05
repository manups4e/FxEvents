using System;

namespace FxEvents.Shared.TypeExtensions
{

    public static class ObjectExtensions
    {
        public static void Clone(this object source, object destination, bool defaults = true)
        {
            Type type = destination.GetType();
            System.Reflection.PropertyInfo[] properties = source.GetType().GetProperties();

            foreach (System.Reflection.PropertyInfo property in properties)
            {
                if (!property.CanRead) continue;

                System.Reflection.PropertyInfo target = type.GetProperty(property.Name);

                if ((target?.CanWrite ?? false) && target.PropertyType.IsAssignableFrom(property.PropertyType))
                {
                    bool primitive = property.PropertyType.IsPrimitive;
                    object value = property.GetValue(source, null);

                    if (!primitive)
                    {
                        value = value.ToJson();
                    }

                    if (!defaults && value == null) continue;

                    target.SetValue(destination, !primitive ? value.ToString().FromJson(property.PropertyType) : value, null);
                }
            }
        }

        public static object Clone(this object source, bool defaults = true)
        {
            object holder = Activator.CreateInstance(source.GetType());

            Clone(source, holder, defaults);

            return holder;
        }
    }
}