namespace Redpoint.Unreal.Serialization
{
    public record class Name : ISerializable<Name>
    {
        public readonly Store<string> StringName;

        public static readonly Name Empty = new Name();

        public Name()
        {
            StringName = new Store<string>(string.Empty);
        }

        public Name(Store<string> name)
        {
            StringName = name;
        }

        public static async Task Serialize(Archive ar, Store<Name> value)
        {
            if (ar.IsLoading)
            {
                var data = new Store<string>(string.Empty);
                await ar.Serialize(data);
                value.V = new Name(data);
            }
            else
            {
                var data = value.V.StringName;
                await ar.Serialize(data, encodeAsASCII: true);
            }
        }

        public override string ToString()
        {
            return StringName.V.ToLowerInvariant();
        }
    }
}
