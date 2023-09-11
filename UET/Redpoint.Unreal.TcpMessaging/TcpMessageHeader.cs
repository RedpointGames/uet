namespace Redpoint.Unreal.TcpMessaging
{
    using Redpoint.Unreal.Serialization;

    public class TcpMessageHeader : ISerializable<TcpMessageHeader>
    {
        private Store<uint> _magicNumber;
        private Store<uint> _version;
        private Store<Guid> _nodeId;

        public const uint DefaultMagicNumber = 0x45504943u;
        public const uint DefaultVersionNumber = 1;
        public const uint DefaultReceiveBufferSize = 2 * 1024 * 1024;
        public const uint DefaultSendBufferSize = 2 * 1024 * 1024;
        public const int DefaultMaxRecipients = 1024;
        public const int DefaultMaxAnnotations = 128;

        public uint MagicNumber => _magicNumber.V;
        public uint Version => _version.V;
        public Guid NodeId => _nodeId.V;

        public TcpMessageHeader() : this(Guid.NewGuid())
        {
        }

        public TcpMessageHeader(Guid Guid)
        {
            _magicNumber = new(DefaultMagicNumber);
            _version = new(DefaultVersionNumber);
            _nodeId = new(Guid);
        }

        public bool IsValid()
        {
            return _magicNumber.V == DefaultMagicNumber &&
                _version.V == DefaultVersionNumber;
        }

        public static async Task Serialize(Archive ar, Store<TcpMessageHeader> value)
        {
            await ar.Serialize(value.V._magicNumber).ConfigureAwait(false);
            await ar.Serialize(value.V._version).ConfigureAwait(false);
            await ar.Serialize(value.V._nodeId).ConfigureAwait(false);
        }
    }
}