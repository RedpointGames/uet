namespace Redpoint.Unreal.Serialization
{
    public record class Store<T>
    {
        protected Store()
        {
            V = default!;
        }

        public Store(T value)
        {
            V = value;
        }

        public virtual T V { get; set; }

        public override string ToString()
        {
            return V!.ToString() ?? string.Empty;
        }
    }
}