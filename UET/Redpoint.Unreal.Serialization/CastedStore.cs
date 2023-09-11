namespace Redpoint.Unreal.Serialization
{
    public record class CastedStore<TAs, TOriginal> : Store<TAs>
    {
        private readonly Store<TOriginal> _underlying = new Store<TOriginal>(default!);

        public CastedStore(Store<TOriginal> underlying) : base()
        {
            if (_underlying == null)
            {
                throw new ArgumentNullException(nameof(underlying));
            }
            _underlying = underlying;
        }

        public override TAs V
        {
            get
            {
                return (TAs)(object)_underlying.V!;
            }
            set
            {
                if (_underlying == null)
                {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                    throw new ArgumentNullException(nameof(_underlying));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
                }
                _underlying.V = (TOriginal)(object)value!;
            }
        }
    }
}