namespace Docker.Registry.DotNet.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class IDictionaryExtensions
    {
        public static string GetQueryString(this IDictionary<string, string[]> values)
        {
            return string.Join(
                "&",
                values.Select(
                    pair => string.Join(
                        "&",
                        pair.Value.Select(
                            v => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(v)}"))));
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            TKey key)
        {
            if (dict.TryGetValue(key, out var value))
                return value;

            return default;
        }
    }
}