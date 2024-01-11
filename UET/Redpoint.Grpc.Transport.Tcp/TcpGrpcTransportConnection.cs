namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using Google.Protobuf;
    using Google.Protobuf.Reflection;
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    internal class TcpGrpcTransportConnection : IAsyncDisposable
    {
        private readonly NetworkStream _networkStream;
        private readonly TcpClient _client;
        private int _pendingReadSize;

        public static async Task<TcpGrpcTransportConnection> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Unable to connect to the remote host."));
            }
            return new TcpGrpcTransportConnection(client);
        }

        public TcpGrpcTransportConnection(TcpClient client)
        {
            _networkStream = client.GetStream();
            _pendingReadSize = 0;
            _client = client;
        }

        public async Task WriteAsync<T>(T value) where T : IMessage
        {
            var length = value.CalculateSize();
            var lengthBuffer = new byte[sizeof(int)];
            using var dataBuffer = MemoryPool<byte>.Shared.Rent(length);
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, length);
            value.WriteTo(dataBuffer.Memory.Span.Slice(0, length));
            await _networkStream.WriteAsync(lengthBuffer).ConfigureAwait(false);
            await _networkStream.WriteAsync(dataBuffer.Memory.Slice(0, length)).ConfigureAwait(false);
        }

        public async Task WriteBlobAsync(byte[] value)
        {
            var length = value.Length;
            var lengthBuffer = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, length);
            await _networkStream.WriteAsync(lengthBuffer).ConfigureAwait(false);
            await _networkStream.WriteAsync(value).ConfigureAwait(false);
        }

        public async Task WriteCloseAsync()
        {
            var length = -1;
            var lengthBuffer = new byte[sizeof(int)];
            using var dataBuffer = MemoryPool<byte>.Shared.Rent(length);
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, length);
            await _networkStream.WriteAsync(lengthBuffer).ConfigureAwait(false);
        }

        public async Task<T> ReadExpectedAsync<T>(
            MessageDescriptor descriptor,
            CancellationToken cancellationToken) where T : IMessage
        {
            if (_pendingReadSize > 0)
            {
                using var discardBuffer = MemoryPool<byte>.Shared.Rent(_pendingReadSize);
                await _networkStream.ReadExactlyAsync(discardBuffer.Memory.Slice(0, _pendingReadSize), cancellationToken).ConfigureAwait(false);
                _pendingReadSize = 0;
            }
            var lengthBuffer = new byte[sizeof(int)];
            await _networkStream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (length < 0)
            {
                await DisposeAsync().ConfigureAwait(false);
                throw new RpcException(new Status(StatusCode.OK, "The connection was closed by the remote host."));
            }
            _pendingReadSize = length;
            using var dataBuffer = MemoryPool<byte>.Shared.Rent(length);
            await _networkStream.ReadExactlyAsync(dataBuffer.Memory.Slice(0, length), cancellationToken).ConfigureAwait(false);
            _pendingReadSize = 0;
            return (T)descriptor.Parser.ParseFrom(dataBuffer.Memory.Span.Slice(0, length));
        }

        private class NestedMemoryOwner : IMemoryOwner<byte>
        {
            private readonly IMemoryOwner<byte> _parent;
            private readonly int _length;
            private readonly Memory<byte> _slice;

            public NestedMemoryOwner(IMemoryOwner<byte> parent, int length)
            {
                _parent = parent;
                _length = length;
                _slice = _parent.Memory.Slice(0, _length);
            }

            public Memory<byte> Memory => _slice;

            public void Dispose()
            {
                _parent.Dispose();
            }
        }

        public async Task<IMemoryOwner<byte>> ReadBlobAsync(
            CancellationToken cancellationToken)
        {
            if (_pendingReadSize > 0)
            {
                using var discardBuffer = MemoryPool<byte>.Shared.Rent(_pendingReadSize);
                await _networkStream.ReadExactlyAsync(discardBuffer.Memory.Slice(0, _pendingReadSize), cancellationToken).ConfigureAwait(false);
                _pendingReadSize = 0;
            }
            var lengthBuffer = new byte[sizeof(int)];
            await _networkStream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (length < 0)
            {
                await DisposeAsync().ConfigureAwait(false);
                throw new RpcException(new Status(StatusCode.OK, "The connection was closed by the remote host."));
            }
            _pendingReadSize = length;
            var dataBuffer = MemoryPool<byte>.Shared.Rent(length);
            try
            {
                await _networkStream.ReadExactlyAsync(
                    dataBuffer.Memory.Slice(0, length),
                    cancellationToken).ConfigureAwait(false);
                _pendingReadSize = 0;
                return new NestedMemoryOwner(dataBuffer, length);
            }
            catch
            {
                dataBuffer.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _networkStream.DisposeAsync().ConfigureAwait(false);
            _client.Close();
            _client.Dispose();
        }
    }
}
