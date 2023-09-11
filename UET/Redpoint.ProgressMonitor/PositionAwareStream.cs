using System;

namespace Redpoint.ProgressMonitor
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps another stream (such as a HTTP response stream) and tracks the progress of <see cref="Read"/> operations so that it can report the current position to a byte-aware monitor.
    /// </summary>
    public class PositionAwareStream : Stream
    {
        private readonly Stream _underlyingStream;
        private readonly long _length;
        private long _position;

        /// <summary>
        /// Creates a <see cref="PositionAwareStream"/> which wraps the target system.
        /// </summary>
        /// <param name="underlyingStream">The stream to wrap.</param>
        /// <param name="length">The length of the underlying stream.</param>
        public PositionAwareStream(Stream underlyingStream, long length)
        {
            _underlyingStream = underlyingStream;
            _length = length;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _underlyingStream.Dispose();
        }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _length;

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            _underlyingStream.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _underlyingStream.Read(buffer, offset, count);
            _position += bytesRead;
            return bytesRead;
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if NETCOREAPP2_1_OR_GREATER
            var bytesRead = await _underlyingStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
#else
            var bytesRead = await _underlyingStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#endif
            _position += bytesRead;
            return bytesRead;
        }

#if NETCOREAPP2_1_OR_GREATER
        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await _underlyingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += bytesRead;
            return bytesRead;
        }
#endif

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
