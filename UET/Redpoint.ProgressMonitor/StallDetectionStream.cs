namespace Redpoint.ProgressMonitor
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps another stream (such as a HTTP response stream) and throws an exception if <see cref="Read"/> operations ever cease returning data for a given timeout without hitting EOF.
    /// </summary>
    public class StallDetectionStream : Stream
    {
        private readonly Stream _underlyingStream;
        private readonly TimeSpan _stallPeriod;
        private DateTimeOffset? _lastTimeDataReceived;

        /// <summary>
        /// Creates a <see cref="StallDetectionStream"/> which wraps the target system.
        /// </summary>
        /// <param name="underlyingStream">The stream to wrap.</param>
        /// <param name="stallPeriod">The maximum amount of time that can elapse without reading data.</param>
        public StallDetectionStream(Stream underlyingStream, TimeSpan stallPeriod)
        {
            _underlyingStream = underlyingStream;
            _stallPeriod = stallPeriod;
            _lastTimeDataReceived = null;
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
        public override long Length => _underlyingStream.Length;

        /// <inheritdoc/>
        public override long Position { get => _underlyingStream.Position; set => _underlyingStream.Position = value; }

        /// <inheritdoc/>
        public override int ReadTimeout { get => (int)_stallPeriod.TotalMilliseconds; set { } }

        /// <inheritdoc/>
        public override int WriteTimeout { get => _underlyingStream.WriteTimeout; set => _underlyingStream.WriteTimeout = value; }

        /// <inheritdoc/>
        public override void Flush()
        {
            _underlyingStream.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_lastTimeDataReceived.HasValue &&
                _lastTimeDataReceived.Value < DateTimeOffset.UtcNow - _stallPeriod)
            {
                throw new StreamStalledException();
            }

            try
            {
                _underlyingStream.ReadTimeout = (int)_stallPeriod.TotalMilliseconds;
            }
            catch (InvalidOperationException)
            {
            }

            var bytesRead = _underlyingStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                _lastTimeDataReceived = DateTimeOffset.UtcNow;
            }
            return bytesRead;
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_lastTimeDataReceived.HasValue &&
                _lastTimeDataReceived.Value < DateTimeOffset.UtcNow - _stallPeriod)
            {
                throw new StreamStalledException();
            }

            using var ctsTimer = new CancellationTokenSource((int)_stallPeriod.TotalMilliseconds);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimer.Token);
                var bytesRead = await _underlyingStream.ReadAsync(buffer.AsMemory(offset, count), cts.Token).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    _lastTimeDataReceived = DateTime.UtcNow;
                }
                return bytesRead;
            }
            catch (InvalidOperationException) when (ctsTimer.IsCancellationRequested)
            {
                throw new StreamStalledException();
            }
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_lastTimeDataReceived.HasValue &&
                _lastTimeDataReceived.Value < DateTimeOffset.UtcNow - _stallPeriod)
            {
                throw new StreamStalledException();
            }

            using var ctsTimer = new CancellationTokenSource((int)_stallPeriod.TotalMilliseconds);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimer.Token);
                var bytesRead = await _underlyingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    _lastTimeDataReceived = DateTime.UtcNow;
                }
                return bytesRead;
            }
            catch (InvalidOperationException) when (ctsTimer.IsCancellationRequested)
            {
                throw new StreamStalledException();
            }
        }

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
