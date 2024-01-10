namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class TcpGrpcDeserializationContext : DeserializationContext
    {
        private readonly Memory<byte> _memory;

        public TcpGrpcDeserializationContext(Memory<byte> memory)
        {
            _memory = memory;
        }

        public override byte[] PayloadAsNewBuffer()
        {
            return _memory.ToArray();
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            return new ReadOnlySequence<byte>(_memory);
        }

        public override int PayloadLength => _memory.Length;
    }
}
