namespace Redpoint.Unreal.Serialization
{
    public record class Name : ISerializable<Name>
    {
        public readonly string StringName;

        public static readonly Name Empty = new Name();

        public Name()
        {
            StringName = string.Empty;
        }

        public Name(string name)
        {
            StringName = name;
        }

        public static void Serialize(Archive ar, ref Name value)
        {
            if (ar.IsLoading)
            {
                string data = string.Empty;
                ar.Serialize(ref data);
                value = new Name(data);
            }
            else
            {
                string data = value.StringName;
                ar.Serialize(ref data, encodeAsASCII: true);
            }
        }

        public static implicit operator Name(string value) => new(value);

        public override string ToString()
        {
            return StringName.ToLowerInvariant();
        }
    }
}
