namespace Redpoint.Unreal.Serialization
{
    using System.Numerics;

    public class ArchiveArray<I, V> : ISerializable<ArchiveArray<I, V>> where I : notnull, IBinaryInteger<I>, new() where V : new()
    {
        public V[] Data { get; private init; }

        public static readonly ArchiveArray<I, V> Empty = new(I.CreateChecked(0));

        public ArchiveArray() : this(I.CreateChecked(0))
        {
        }

        public ArchiveArray(I length)
        {
            Data = new V[int.CreateChecked(length)];
        }

        public ArchiveArray(IEnumerable<V> values)
        {
            Data = values.ToArray();
        }

        public static void Serialize(Archive ar, ref ArchiveArray<I, V> value)
        {
            if (ar.IsLoading)
            {
                I idx = new();
                ar.RuntimeSerialize(ref idx);
                value = new ArchiveArray<I, V>(idx);
                for (I i = I.CreateChecked(0); i < idx; i++)
                {
                    value.Data[int.CreateChecked(i)] = new V();
                    ar.RuntimeSerialize(ref value.Data[int.CreateChecked(i)]);
                }
            }
            else
            {
                I idx = I.CreateChecked(value.Data.Length);
                ar.RuntimeSerialize(ref idx);
                for (I i = I.CreateChecked(0); i < idx; i++)
                {
                    ar.RuntimeSerialize(ref value.Data[int.CreateChecked(i)]);
                }
            }
        }
    }
}
