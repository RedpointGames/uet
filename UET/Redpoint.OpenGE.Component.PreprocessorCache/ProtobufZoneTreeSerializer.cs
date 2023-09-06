namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Google.Protobuf;
    using Tenray.ZoneTree.Serializers;

    internal class ProtobufZoneTreeSerializer<T> : ISerializer<T> where T : IMessage<T>, new()
    {
        private static readonly T _t = new();

        public T Deserialize(byte[] bytes)
        {
            if (bytes.Length > 32 * 1024)
            {
                var stream = CodedInputStream.CreateWithLimits(new MemoryStream(bytes), int.MaxValue, int.MaxValue);
                return (T)_t.Descriptor.Parser.ParseFrom(stream);
            }
            else
            {
                return (T)_t.Descriptor.Parser.ParseFrom(bytes);
            }
        }

        public byte[] Serialize(in T entry)
        {
            return entry.ToByteArray();
        }
    }
}
