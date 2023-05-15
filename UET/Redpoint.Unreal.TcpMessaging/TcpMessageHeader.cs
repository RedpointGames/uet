namespace Redpoint.Unreal.TcpMessaging
{
    using Redpoint.Unreal.Serialization;

    public class TcpMessageHeader : ISerializable<TcpMessageHeader>
    {
        private uint _magicNumber;
        private uint _version;
        private Guid _nodeId;

        public const uint DefaultMagicNumber = 0x45504943u;
        public const uint DefaultVersionNumber = 1;
        public const uint DefaultReceiveBufferSize = 2 * 1024 * 1024;
        public const uint DefaultSendBufferSize = 2 * 1024 * 1024;
        public const int DefaultMaxRecipients = 1024;
        public const int DefaultMaxAnnotations = 128;

        public uint MagicNumber => _magicNumber;
        public uint Version => _version;
        public Guid NodeId => _nodeId;

        public TcpMessageHeader() : this(Guid.NewGuid())
        {
        }

        public TcpMessageHeader(Guid Guid)
        {
            _magicNumber = DefaultMagicNumber;
            _version = DefaultVersionNumber;
            _nodeId = Guid;
        }

        public bool IsValid()
        {
            return _magicNumber == DefaultMagicNumber &&
                _version == DefaultVersionNumber;
        }

        public static void Serialize(Archive ar, ref TcpMessageHeader value)
        {
            ar.Serialize(ref value._magicNumber);
            ar.Serialize(ref value._version);
            ar.Serialize(ref value._nodeId);
        }
    }
}