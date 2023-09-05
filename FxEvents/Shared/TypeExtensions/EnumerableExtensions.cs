using System;
using System.Collections.Generic;
using System.Linq;


namespace FxEvents.Shared.TypeExtensions
{

    public static class EnumerableExtensions
    {
        public static string GetString(this List<object> list, int index)
        {
            return list[index].ToString();
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (T value in enumerable)
            {
                action?.Invoke(value);
            }
        }

        public static IEnumerable<T> Aggregate<T>(this IEnumerable<T> enumerable, Action<T> aggregator)
        {
            List<T> aggregate = enumerable.ToList();

            aggregate.ForEach(self => aggregator?.Invoke(self));

            return aggregate;
        }

        public static IEnumerable<Tuple<T, int>> WithIndex<T>(this IEnumerable<T> enumerable)
        {
            List<Tuple<T, int>> collection = new List<Tuple<T, int>>();
            int index = -1;

            foreach (T entry in enumerable)
            {
                index++;
                collection.Add(Tuple.Create(entry, index));
            }

            return collection;
        }

        public static IEnumerable<T> Replace<T>(this IEnumerable<T> enumerable, Func<T, bool> filter, T replacement)
        {
            foreach (T value in enumerable)
            {
                bool condition = filter.Invoke(value);

                yield return condition ? replacement : value;
            }
        }

        public static void Replace<T>(this List<T> list, Func<T, bool> filter, T replacement)
        {
            int index = -1;

            for (int i = 0; i < list.Count; i++)
            {
                T entry = list[i];

                if (filter.Invoke(entry))
                {
                    index = i;
                }
            }

            if (index != -1)
                list[index] = replacement;
        }

        public static string Collect<T>(this IEnumerable<T> enumerable, string separator = null)
        {
            return string.Join(separator ?? string.Empty, enumerable);
        }

        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue def, Func<TValue, TValue> mutation)
        {
            if (dictionary.TryGetValue(key, out TValue? value))
            {
                dictionary[key] = mutation.Invoke(value);
            }
            else
            {
                dictionary.Add(key, def);
            }
        }
    }
}