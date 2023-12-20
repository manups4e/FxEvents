using System;


namespace FxEvents.Shared.TypeExtensions
{

    public static class StringExtensions
    {
        public static bool Contains(this string source, string target, StringComparison comparison)
        {
            return source?.IndexOf(target, comparison) >= 0;
        }

        public static string Remove(this string source, string pattern)
        {
            return source.Replace(pattern, string.Empty);
        }

        public static string Surround(this string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) ? value + source + value : string.Empty;
        }

        public static int ToInt(this string source)
        {
            return int.Parse(source);
        }

        public static float ToFloat(this string source)
        {
            return float.Parse(source);
        }

        public static bool ToBool(this string source)
        {
            if (int.TryParse(source, out int number))
            {
                return number >= 1;
            }

            return bool.Parse(source);
        }
    }
}