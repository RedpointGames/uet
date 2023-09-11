namespace Redpoint.OpenGE.Core.ReadableStream
{
    using Google.Protobuf;
    using Grpc.Core;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of <see cref="Stream"/> that takes a stream
    /// of incoming gRPC messages and reconstructs a binary stream of
    /// data from the messages as you read from the stream.
    /// </summary>
    /// <typeparam name="TInbound">This type isn't used, but is necessary to provide the <see cref="GrpcReadableBinaryChunkStream(BufferedAsyncDuplexStreamingCall{TInbound, TOutbound})"/> constructor.</typeparam>
    /// <typeparam name="TOutbound">The type of messages being received from the remote peer over the gRPC streaming call.</typeparam>
    public abstract class GrpcReadableBinaryChunkStream<TInbound, TOutbound> : Stream where TOutbound : class
    {
        private readonly BufferedAsyncDuplexStreamingCall<TInbound, TOutbound>? _bufferedStream;
        private readonly IAsyncStreamReader<TOutbound>? _receivingStream;
        private bool _needsNextItem;
        private TOutbound? _current;
        private int _currentPosition;
        private bool _finished;
        private long _globalPosition;

        protected GrpcReadableBinaryChunkStream(
            BufferedAsyncDuplexStreamingCall<TInbound, TOutbound> stream)
        {
            _bufferedStream = stream;
            _receivingStream = null;
            _needsNextItem = true;
            _current = null;
            _currentPosition = 0;
            _globalPosition = 0;
            _finished = false;
        }

        protected GrpcReadableBinaryChunkStream(
            TOutbound initial,
            IAsyncStreamReader<TOutbound> receivingStream)
        {
            _bufferedStream = null;
            _receivingStream = receivingStream;
            _needsNextItem = false;
            _current = initial;
            _currentPosition = 0;
            _globalPosition = 0;
            _finished = false;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        protected abstract void ValidateOutbound(TOutbound outbound);

        protected abstract ByteString GetData(TOutbound outbound);

        protected abstract bool GetFinished(TOutbound outbound);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_finished)
            {
                return 0;
            }

            var read = 0;
            while (read < buffer.Length)
            {
                if (_needsNextItem)
                {
                    if (_bufferedStream != null)
                    {
                        _current = await _bufferedStream.GetNextAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!await _receivingStream!.MoveNext(cancellationToken).ConfigureAwait(false))
                        {
                            // The stream is closed.
                            _finished = true;
                            return read;
                        }
                        _current = _receivingStream.Current;
                    }
                    _currentPosition = 0;
                    _needsNextItem = false;

                    ValidateOutbound(_current!);
                }

                var data = GetData(_current!);
                var finished = GetFinished(_current!);

                var toRead = Math.Min(
                    buffer.Length - read,
                    data.Length - _currentPosition);
                data.Memory
                    .Slice(
                        _currentPosition,
                        toRead)
                    .CopyTo(buffer);
                _currentPosition += toRead;
                _globalPosition += toRead;
                read += toRead;
                if (_currentPosition == data.Length)
                {
                    if (finished)
                    {
                        _finished = true;
                        return read;
                    }
                    else
                    {
                        _needsNextItem = true;
                        continue;
                    }
                }
                else if (_currentPosition > data.Length)
                {
                    throw new InvalidOperationException();
                }
            }

            return read;
        }

        #region Unsupported Methods

        public override long Length => throw new NotSupportedException();

        public override long Position { get => _globalPosition; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
