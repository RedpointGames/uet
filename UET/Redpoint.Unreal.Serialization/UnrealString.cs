namespace Redpoint.Unreal.Serialization
{
    using System.Diagnostics.CodeAnalysis;

    public record class UnrealString : ISerializable<UnrealString>
    {
        public Store<string> Value;

        public UnrealString()
        {
            Value = new Store<string>(string.Empty);
        }

        public UnrealString(string name)
        {
            Value = new Store<string>(name);
        }

        public static async Task Serialize(Archive ar, Store<UnrealString> value)
        {
            if (ar == null) throw new ArgumentNullException(nameof(ar));
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ar.Serialize(value.V.Value).ConfigureAwait(false);
        }

        [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "The alternative to this operator is to use the constructor instead.")]
        public static implicit operator UnrealString(string value) => new(value);

        public static implicit operator string(UnrealString value)
        {
            if (value == null) return string.Empty;
            return value.Value.V;
        }
    }
}
