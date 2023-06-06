namespace Redpoint.Unreal.Serialization
{
    using System.Numerics;

    public class ArchiveMap<I, K, V> : ISerializable<ArchiveMap<I, K, V>> where I : notnull, IBinaryInteger<I>, new() where K : notnull, new() where V : new()
    {
        public Dictionary<K, V> Data { get; }

        public static readonly ArchiveMap<I, K, V> Empty = new();

        public ArchiveMap()
        {
            Data = new Dictionary<K, V>();
        }

        public ArchiveMap(IEnumerable<KeyValuePair<K, V>> values)
        {
            Data = values.ToDictionary(k => k.Key, v => v.Value);
        }

        public static async Task Serialize(Archive ar, Store<ArchiveMap<I, K, V>> value)
        {
            if (ar.IsLoading)
            {
                var idx = new Store<I>(I.Zero);
                await ar.RuntimeSerialize(idx);
                value.V = new ArchiveMap<I, K, V>();
                for (I i = I.CreateChecked(0); i < idx.V; i++)
                {
                    var k = new Store<K>(new K());
                    var v = new Store<V>(new V());
                    await ar.RuntimeSerialize(k);
                    await ar.RuntimeSerialize(v);
                    if (k.V == null || v.V == null)
                    {
                        throw new NullReferenceException("Invalid deserializer logic!");
                    }
                    value.V.Data.Add(k.V, v.V);
                }
            }
            else
            {
                var idx = new Store<I>(I.CreateChecked(value.V.Data.Count));
                await ar.RuntimeSerialize(idx);
                foreach (var kv in value.V.Data)
                {
                    var k = new Store<K>(kv.Key);
                    var v = new Store<V>(kv.Value);
                    await ar.RuntimeSerialize(k);
                    await ar.RuntimeSerialize(v);
                }
            }
        }
    }
}
