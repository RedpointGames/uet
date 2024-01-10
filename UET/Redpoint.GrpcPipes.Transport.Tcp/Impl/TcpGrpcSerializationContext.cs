namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using System;
    using System.Buffers;

    internal class TcpGrpcSerializationContext : SerializationContext
    {
        private readonly ArrayBufferWriter<byte> _writer;

        public byte[] Result { get; private set; } = Array.Empty<byte>();

        public TcpGrpcSerializationContext()
        {
            _writer = new ArrayBufferWriter<byte>();
        }

        public override void SetPayloadLength(int payloadLength)
        {
        }

        public override void Complete()
        {
            Result = _writer.WrittenMemory.ToArray();
        }

        public override void Complete(byte[] payload)
        {
            _writer.Write(payload);
            Result = _writer.WrittenMemory.ToArray();
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer;
        }
    }
}
