namespace Redpoint.Unreal.Serialization
{
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
            await ar.Serialize(value.V.Value);
        }

        public static implicit operator UnrealString(string value) => new(value);
        public static implicit operator string(UnrealString value) => value.Value.V;
    }
}
