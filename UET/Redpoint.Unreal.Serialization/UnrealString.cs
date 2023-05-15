namespace Redpoint.Unreal.Serialization
{
    public record class UnrealString : ISerializable<UnrealString>
    {
        public string Value;

        public UnrealString()
        {
            Value = string.Empty;
        }

        public UnrealString(string name)
        {
            Value = name;
        }

        public static void Serialize(Archive ar, ref UnrealString value)
        {
            ar.Serialize(ref value.Value);
        }

        public static implicit operator UnrealString(string value) => new(value);
        public static implicit operator string(UnrealString value) => value.Value;
    }
}
