namespace Redpoint.Unreal.Serialization
{
    public record class MessageAddress : ISerializable<MessageAddress>
    {
        public Store<Guid> UniqueId;

        public MessageAddress()
        {
            UniqueId = new Store<Guid>(Guid.NewGuid());
        }

        public MessageAddress(Guid uniqueId)
        {
            UniqueId = new Store<Guid>(uniqueId);
        }

        public static async Task Serialize(Archive ar, Store<MessageAddress> value)
        {
            if (ar == null) throw new ArgumentNullException(nameof(ar));
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ar.Serialize(value.V.UniqueId).ConfigureAwait(false);
        }
    }
}
