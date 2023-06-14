namespace Docker.Registry.DotNet.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class QueryString : IQueryString
    {
        private readonly Dictionary<string, string[]> _values = new Dictionary<string, string[]>();

        public string GetQueryString()
        {
            return string.Join(
                "&",
                this._values.Select(
                    pair => string.Join(
                        "&",
                        pair.Value.Select(
                            v => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(v)}"))));
        }

        public void Add(string key, string value)
        {
            this._values.Add(key, new[] { value });
        }

        public void Add(string key, string[] values)
        {
            this._values.Add(key, values);
        }
    }
}