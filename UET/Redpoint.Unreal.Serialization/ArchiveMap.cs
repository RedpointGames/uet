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

        public static void Serialize(Archive ar, ref ArchiveMap<I, K, V> value)
        {
            if (ar.IsLoading)
            {
                I idx = new();
                ar.RuntimeSerialize(ref idx!);
                value = new ArchiveMap<I, K, V>();
                for (I i = I.CreateChecked(0); i < idx; i++)
                {
                    K k = new();
                    V v = new();
                    ar.RuntimeSerialize(ref k);
                    ar.RuntimeSerialize(ref v);
                    if (k == null || v == null)
                    {
                        throw new NullReferenceException("Invalid deserializer logic!");
                    }
                    value.Data.Add(k, v);
                }
            }
            else
            {
                I idx = I.CreateChecked(value.Data.Count);
                ar.RuntimeSerialize(ref idx);
                foreach (var kv in value.Data)
                {
                    K k = kv.Key;
                    V v = kv.Value;
                    ar.RuntimeSerialize(ref k);
                    ar.RuntimeSerialize(ref v);
                }
            }
        }
    }
}
