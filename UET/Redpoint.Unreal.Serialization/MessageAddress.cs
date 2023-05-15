namespace Redpoint.Unreal.Serialization
{
    public record class MessageAddress : ISerializable<MessageAddress>
    {
        public Guid UniqueId;

        public MessageAddress()
        {
            UniqueId = Guid.NewGuid();
        }

        public MessageAddress(Guid uniqueId)
        {
            UniqueId = uniqueId;
        }

        public static void Serialize(Archive ar, ref MessageAddress value)
        {
            ar.Serialize(ref value.UniqueId);
        }
    }
}
