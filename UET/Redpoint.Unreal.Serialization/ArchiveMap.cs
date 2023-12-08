namespace Redpoint.Unreal.Serialization
{
    using System;
    using System.Numerics;

    public class ArchiveMap<TIndex, TKey, TValue> : ISerializable<ArchiveMap<TIndex, TKey, TValue>> where TIndex : notnull, IBinaryInteger<TIndex>, new() where TKey : notnull, new() where TValue : new()
    {
        public Dictionary<TKey, TValue> Data { get; }

        public static readonly ArchiveMap<TIndex, TKey, TValue> Empty = new();

        public ArchiveMap()
        {
            Data = new Dictionary<TKey, TValue>();
        }

        public ArchiveMap(IEnumerable<KeyValuePair<TKey, TValue>> values)
        {
            Data = values.ToDictionary(k => k.Key, v => v.Value);
        }

        public static async Task Serialize(Archive ar, Store<ArchiveMap<TIndex, TKey, TValue>> value)
        {
            ArgumentNullException.ThrowIfNull(ar);
            ArgumentNullException.ThrowIfNull(value);

            if (ar.IsLoading)
            {
                var idx = new Store<TIndex>(TIndex.Zero);
                await ar.RuntimeSerialize(idx).ConfigureAwait(false);
                value.V = new ArchiveMap<TIndex, TKey, TValue>();
                for (TIndex i = TIndex.CreateChecked(0); i < idx.V; i++)
                {
                    var k = new Store<TKey>(new TKey());
                    var v = new Store<TValue>(new TValue());
                    await ar.RuntimeSerialize(k).ConfigureAwait(false);
                    await ar.RuntimeSerialize(v).ConfigureAwait(false);
                    if (k.V == null || v.V == null)
                    {
                        throw new InvalidOperationException("Invalid deserializer logic!");
                    }
                    value.V.Data.Add(k.V, v.V);
                }
            }
            else
            {
                var idx = new Store<TIndex>(TIndex.CreateChecked(value.V.Data.Count));
                await ar.RuntimeSerialize(idx).ConfigureAwait(false);
                foreach (var kv in value.V.Data)
                {
                    var k = new Store<TKey>(kv.Key);
                    var v = new Store<TValue>(kv.Value);
                    await ar.RuntimeSerialize(k).ConfigureAwait(false);
                    await ar.RuntimeSerialize(v).ConfigureAwait(false);
                }
            }
        }
    }
}
