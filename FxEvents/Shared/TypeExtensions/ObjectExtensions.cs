using System;

namespace FxEvents.Shared.TypeExtensions
{

    public static class ObjectExtensions
    {
        public static void Clone(this object source, object destination, bool defaults = true)
        {
            var type = destination.GetType();
            var properties = source.GetType().GetProperties();

            foreach (var property in properties)
            {
                if (!property.CanRead) continue;

                var target = type.GetProperty(property.Name);

                if ((target?.CanWrite ?? false) && target.PropertyType.IsAssignableFrom(property.PropertyType))
                {
                    var primitive = property.PropertyType.IsPrimitive;
                    var value = property.GetValue(source, null);

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
            var holder = Activator.CreateInstance(source.GetType());

            Clone(source, holder, defaults);

            return holder;
        }
    }
}