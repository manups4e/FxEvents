using System.Collections.Generic;
using System.Linq;

namespace FxEvents.Shared.TypeExtensions
{
    public static class KeyValuePairExtensions
    {
        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> list)
        {
            return list.ToDictionary(x => x.Key, x => x.Value);
        }
    }

}