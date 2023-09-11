namespace Redpoint.Unreal.Serialization
{
    using System.Numerics;

    public class ArchiveArray<TIndex, TValue> : ISerializable<ArchiveArray<TIndex, TValue>> where TIndex : notnull, IBinaryInteger<TIndex>, new() where TValue : new()
    {
        private TValue[] _data;

        public IReadOnlyList<TValue> Data => _data;

        public static readonly ArchiveArray<TIndex, TValue> Empty = new(TIndex.CreateChecked(0));

        public ArchiveArray() : this(TIndex.CreateChecked(0))
        {
        }

        public ArchiveArray(TIndex length)
        {
            _data = new TValue[int.CreateChecked(length)];
        }

        public ArchiveArray(IEnumerable<TValue> values)
        {
            _data = values.ToArray();
        }

        public static async Task Serialize(Archive ar, Store<ArchiveArray<TIndex, TValue>> value)
        {
            if (ar == null) throw new ArgumentNullException(nameof(ar));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (ar.IsLoading)
            {
                var idx = new Store<TIndex>(TIndex.Zero);
                await ar.RuntimeSerialize(idx).ConfigureAwait(false);
                value.V = new ArchiveArray<TIndex, TValue>(idx.V);
                for (TIndex i = TIndex.CreateChecked(0); i < idx.V; i++)
                {
                    value.V._data[int.CreateChecked(i)] = new TValue();
                    var store = new Store<TValue>(value.V._data[int.CreateChecked(i)]);
                    await ar.RuntimeSerialize(store).ConfigureAwait(false);
                    value.V._data[int.CreateChecked(i)] = store.V;
                }
            }
            else
            {
                var idx = new Store<TIndex>(TIndex.CreateChecked(value.V._data.Length));
                await ar.RuntimeSerialize(idx).ConfigureAwait(false);
                for (TIndex i = TIndex.CreateChecked(0); i < idx.V; i++)
                {
                    await ar.RuntimeSerialize(new Store<TValue>(value.V._data[int.CreateChecked(i)])).ConfigureAwait(false);
                }
            }
        }
    }
}
