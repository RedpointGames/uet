namespace Redpoint.OpenGE.Core.WritableStream
{
    using Grpc.Core;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of <see cref="Stream"/> that receives writes and converts them into chunked gRPC messages to then send to the RPC streaming endpoint.
    /// </summary>
    /// <typeparam name="TInbound">The type of messages being sent to the remote peer over the gRPC streaming call."/> constructor.</typeparam>
    public abstract class GrpcWritableBinaryChunkStream<TInbound> : Stream
    {
        private readonly IAsyncStreamWriter<TInbound> _sendingStream;
        private readonly Concurrency.Semaphore _writingSemaphore;
        private readonly byte[] _memoryBuffer;
        private int _memoryBufferPosition;
        private long _position;
        private bool _hasSentFinish;

        protected GrpcWritableBinaryChunkStream(
            IAsyncStreamWriter<TInbound> sendingStream)
        {
            _sendingStream = sendingStream;
            _writingSemaphore = new Concurrency.Semaphore(1);
            _memoryBuffer = new byte[128 * 1024];
            _memoryBufferPosition = 0;
            _position = 0;
            _hasSentFinish = false;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        protected abstract TInbound ConstructForSending(
            ReadOnlyMemory<byte> data,
            long position,
            bool isFinished);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(
                new ReadOnlyMemory<byte>(buffer, offset, count),
                cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_hasSentFinish, this);

            if (buffer.Length == 0)
            {
                return;
            }
            var remainingInIncomingBuffer = buffer.Length;
            await _writingSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_hasSentFinish, this);

                var incomingBufferOffset = 0;
                do
                {
                    var remainingInMemoryBuffer = _memoryBuffer.Length - _memoryBufferPosition;
                    var bytesToWrite = Math.Min(
                        remainingInMemoryBuffer,
                        remainingInIncomingBuffer);
                    buffer
                        .Slice(
                            incomingBufferOffset,
                            bytesToWrite)
                        .CopyTo(new Memory<byte>(
                            _memoryBuffer,
                            _memoryBufferPosition,
                            bytesToWrite));
                    _memoryBufferPosition += bytesToWrite;
                    incomingBufferOffset += bytesToWrite;
                    remainingInIncomingBuffer -= bytesToWrite;

                    if (_memoryBufferPosition == _memoryBuffer.Length)
                    {
                        // We need to flush now.
                        var message = ConstructForSending(
                            new ReadOnlyMemory<byte>(_memoryBuffer, 0, _memoryBuffer.Length),
                            _position,
                            false);
                        await _sendingStream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
                        _position += _memoryBuffer.Length;
                        _memoryBufferPosition = 0;
                    }
                    else
                    {
                        // We did a partial write into the buffer.
                        break;
                    }
                } while (incomingBufferOffset < buffer.Length);
            }
            finally
            {
                _writingSemaphore.Release();
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            // @todo: Should we actually flush the buffer to the remote?
            return Task.CompletedTask;
        }

        [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "Stream.DisposeAsync calls into the synchronise Dispose method.")]
        public override async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            await _writingSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!_hasSentFinish)
                {
                    var message = ConstructForSending(
                        new ReadOnlyMemory<byte>(_memoryBuffer, 0, _memoryBufferPosition),
                        _position,
                        true);
                    await _sendingStream.WriteAsync(message).ConfigureAwait(false);
                    _hasSentFinish = true;
                }
            }
            finally
            {
                _writingSemaphore.Release();
            }
        }

        #region Unsupported Methods

        public override long Length => throw new NotSupportedException();

        public override long Position { get => _position + _memoryBufferPosition; set => throw new NotSupportedException(); }

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

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new NotSupportedException("GrpcWritableBinaryChunkStream must be disposed asynchronously using DisposeAsync");
        }

        #endregion
    }
}
