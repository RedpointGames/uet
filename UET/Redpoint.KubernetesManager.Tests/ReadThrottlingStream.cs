namespace Redpoint.KubernetesManager.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps a stream that will be read from, and throttles the data read rate to a maximum number of bytes per second.
    /// </summary>
    internal class ReadThrottlingStream : Stream
    {
        private readonly Stream _source;
        private readonly long _maximumBytesPerSecond;
        private readonly DateTimeOffset _start;
        private long _bytesRead;
        private readonly DateTimeOffset? _stallAt;

        public ReadThrottlingStream(
            Stream source,
            long maximumBytesPerSecond,
            TimeSpan? stallAfter)
        {
            _source = source;
            _maximumBytesPerSecond = maximumBytesPerSecond;
            _start = DateTimeOffset.UtcNow;
            _bytesRead = 0;
            _stallAt = stallAfter == null ? null : (_start + stallAfter);
        }

        private long CapBytesRead => (long)Math.Ceiling((DateTimeOffset.UtcNow - _start).TotalSeconds * _maximumBytesPerSecond);

        private long AllowedBytesRead => (_stallAt.HasValue && DateTimeOffset.UtcNow > _stallAt) ? 0 : Math.Max(0, CapBytesRead - _bytesRead);

        public override bool CanRead => _source.CanRead;

        public override bool CanSeek => _source.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _source.Length;

        public override long Position { get => _source.Position; set => _source.Position = value; }

        public override void Flush()
        {
            _source.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _source.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _source.Read(buffer, offset, (int)Math.Min(AllowedBytesRead, count));
            _bytesRead += read;
            return read;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return _source.BeginRead(buffer, offset, (int)Math.Min(AllowedBytesRead, count), callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            var read = _source.EndRead(asyncResult);
            _bytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _source.Read(buffer.Slice(0, (int)Math.Min(AllowedBytesRead, buffer.Length)));
            _bytesRead += read;
            return read;
        }

        [SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "Proxying.")]
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _source.ReadAsync(buffer, offset, (int)Math.Min(AllowedBytesRead, count), cancellationToken);
            _bytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _source.ReadAsync(buffer.Slice(0, (int)Math.Min(AllowedBytesRead, buffer.Length)), cancellationToken);
            _bytesRead += read;
            return read;
        }

        public override int ReadByte()
        {
            return _source.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _source.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _source.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            throw new NotSupportedException();
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            {
                var bytesReadAtEndOfOperation = _bytesRead + bufferSize;
                var requiredTimeElapsed = (_stallAt.HasValue && DateTimeOffset.UtcNow > _stallAt) ? DateTimeOffset.MaxValue : (_start + TimeSpan.FromSeconds(bytesReadAtEndOfOperation / (double)_maximumBytesPerSecond));
                var timeToWait = requiredTimeElapsed - DateTimeOffset.UtcNow;
                if (timeToWait.TotalMilliseconds > 0)
                {
                    Thread.Sleep((int)Math.Ceiling(timeToWait.TotalSeconds));
                }
            }
            var amountRead = _source.Read(buffer, 0, buffer.Length);
            _bytesRead += amountRead;
            do
            {
                destination.Write(buffer, 0, amountRead);

                {
                    var bytesReadAtEndOfOperation = _bytesRead + bufferSize;
                    var requiredTimeElapsed = (_stallAt.HasValue && DateTimeOffset.UtcNow > _stallAt) ? DateTimeOffset.MaxValue : (_start + TimeSpan.FromSeconds(bytesReadAtEndOfOperation / (double)_maximumBytesPerSecond));
                    var timeToWait = requiredTimeElapsed - DateTimeOffset.UtcNow;
                    if (timeToWait.TotalMilliseconds > 0)
                    {
                        Thread.Sleep((int)Math.Ceiling(timeToWait.TotalSeconds));
                    }
                }
                amountRead = _source.Read(buffer, 0, buffer.Length);
                _bytesRead += amountRead;
            } while (amountRead > 0);
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            {
                var bytesReadAtEndOfOperation = _bytesRead + bufferSize;
                var requiredTimeElapsed = (_stallAt.HasValue && DateTimeOffset.UtcNow > _stallAt) ? DateTimeOffset.MaxValue : (_start + TimeSpan.FromSeconds(bytesReadAtEndOfOperation / (double)_maximumBytesPerSecond));
                var timeToWait = requiredTimeElapsed - DateTimeOffset.UtcNow;
                if (timeToWait.TotalMilliseconds > 0)
                {
                    await Task.Delay((int)Math.Ceiling(timeToWait.TotalSeconds), cancellationToken);
                }
            }
            var amountRead = await _source.ReadAsync(buffer, cancellationToken);
            _bytesRead += amountRead;
            do
            {
                await destination.WriteAsync(buffer.AsMemory(0, amountRead), cancellationToken);

                {
                    var bytesReadAtEndOfOperation = _bytesRead + bufferSize;
                    var requiredTimeElapsed = (_stallAt.HasValue && DateTimeOffset.UtcNow > _stallAt) ? DateTimeOffset.MaxValue : (_start + TimeSpan.FromSeconds(bytesReadAtEndOfOperation / (double)_maximumBytesPerSecond));
                    var timeToWait = requiredTimeElapsed - DateTimeOffset.UtcNow;
                    if (timeToWait.TotalMilliseconds > 0)
                    {
                        await Task.Delay((int)Math.Ceiling(timeToWait.TotalSeconds), cancellationToken);
                    }
                }
                amountRead = await _source.ReadAsync(buffer, cancellationToken);
                _bytesRead += amountRead;
            } while (amountRead > 0);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _source.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _source.DisposeAsync();
        }
    }
}
