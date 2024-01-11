namespace Redpoint.Grpc.Transport.Tcp
{
    using Google.Protobuf;
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    internal class TcpClientConnection : IDisposable
    {
        private readonly IPEndPoint _endpoint;
        private TcpClient? _client;

        public TcpClientConnection(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_endpoint, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task WriteCallRequest(TcpCall call)
        {
            using (var memoryStream = new MemoryStream())
            {
                call.WriteTo(memoryStream);
                var length = memoryStream.Position;
                var lengthBuffer = new byte[sizeof(ulong)];
                BinaryPrimitives.WriteInt64BigEndian(lengthBuffer, length);
            }
            using (var memory = MemoryPool<byte>.Shared.Rent(4096))
            {
                var output = new CodedOutputStream(memory.Memor);
                call.WriteTo()
        }

            public void Dispose()
            {
                if (_client != null)
                {
                    _client.Dispose();
                }
            }
        }
    }
