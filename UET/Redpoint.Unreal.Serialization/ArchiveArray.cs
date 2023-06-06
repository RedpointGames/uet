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

        public static async Task Serialize(Archive ar, Store<ArchiveArray<I, V>> value)
        {
            if (ar.IsLoading)
            {
                var idx = new Store<I>(I.Zero);
                await ar.RuntimeSerialize(idx);
                value.V = new ArchiveArray<I, V>(idx.V);
                for (I i = I.CreateChecked(0); i < idx.V; i++)
                {
                    value.V.Data[int.CreateChecked(i)] = new V();
                    var store = new Store<V>(value.V.Data[int.CreateChecked(i)]);
                    await ar.RuntimeSerialize(store);
                    value.V.Data[int.CreateChecked(i)] = store.V;
                }
            }
            else
            {
                var idx = new Store<I>(I.CreateChecked(value.V.Data.Length));
                await ar.RuntimeSerialize(idx);
                for (I i = I.CreateChecked(0); i < idx.V; i++)
                {
                    await ar.RuntimeSerialize(new Store<V>(value.V.Data[int.CreateChecked(i)]));
                }
            }
        }
    }
}
