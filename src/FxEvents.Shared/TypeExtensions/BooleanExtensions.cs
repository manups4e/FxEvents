

namespace FxEvents.Shared.TypeExtensions
{

    public static class BooleanExtensions
    {
        public static void Toggle(this ref bool value)
        {
            value = !value;
        }
    }
}