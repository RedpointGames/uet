namespace Redpoint.CloudFramework.CLI
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(LanguageDictionaryJsonConverter))]
    internal class LanguageDictionary : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly Dictionary<string, string> _dict;
        private readonly List<string> _keys;

        public LanguageDictionary()
        {
            _dict = new Dictionary<string, string>();
            _keys = new List<string>();
        }

        public string this[string key]
        {
            get => _dict[key];
        }

        public IReadOnlyList<string> Keys => _keys;

        public void SortKeys()
        {
            _keys.Sort((a, b) => string.Compare(a, b, StringComparison.Ordinal));
        }

        public int Count => _dict.Count;

        public bool IsSorted()
        {
            string? lastKey = null;
            foreach (var key in _keys)
            {
                if (lastKey != null && string.Compare(lastKey, key, StringComparison.Ordinal) != -1)
                {
                    return false;
                }
                lastKey = key;
            }
            return true;
        }

        public void Add(string k, string v)
        {
            _dict.Add(k, v);
            _keys.Add(k);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var key in _keys)
            {
                yield return new KeyValuePair<string, string>(key, _dict[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
