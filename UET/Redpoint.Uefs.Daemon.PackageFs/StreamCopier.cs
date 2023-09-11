// #define ENABLE_TRACE
// #define ENABLE_STALL_DEBUG

using System;

namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Redpoint.Hashing;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using System.Security.Cryptography;

    internal sealed class StreamCopier : IDisposable
    {
        // This class does not own these disposable objects.
#pragma warning disable CA2213
        private IRemoteStorageBlob _source;
        private Stream _target;
#pragma warning restore CA2213
        private SHA256? _hasher;

        const int _ringBufferLength = 64;
        const int _bufferLength = 1024 * 1024;

        private byte[][] _bufferRings;
        private int[] _bufferBytesRead;
        private int _readPosition = 0;
        private int _writePosition = 0;
        private bool _readComplete = false;
        private bool _started = false;
        private bool _complete = false;

        public StreamCopier(IRemoteStorageBlob source, Stream target, bool enableHashing)
        {
            _source = source;
            _target = target;
            if (enableHashing)
            {
                _hasher = SHA256.Create();
            }

            _bufferRings = new byte[_ringBufferLength][];
            _bufferBytesRead = new int[_ringBufferLength];
            for (int i = 0; i < _ringBufferLength; i++)
            {
                _bufferRings[i] = new byte[_bufferLength];
                _bufferBytesRead[i] = 0;
            }
        }

        public long Position => _source.Position;

        public long Length => _source.Length;

        public string SHA256Hash
        {
            get
            {
                if (_hasher == null)
                {
                    throw new InvalidOperationException("Can't access SHA256Hash property if hashing is not enabled.");
                }
                if (!_complete)
                {
                    throw new InvalidOperationException("SHA256 hash can only be accessed after copy is complete!");
                }

                return Hash.HexString(_hasher.Hash!);
            }
        }

        public async Task CopyAsync()
        {
            if (_complete || _started)
            {
                throw new InvalidOperationException("Can't call CopyAsync twice!");
            }
            _started = true;
            await Task.WhenAll(
                Task.Run(PerformReads),
                Task.Run(PerformWrites)).ConfigureAwait(false);
            _complete = true;
        }

        public void Dispose()
        {
            _hasher?.Dispose();
        }

        private async Task PerformReads()
        {
            do
            {
#if ENABLE_TRACE
                Console.WriteLine($"PerformReads:  Reading into buffer {_readPosition}.");
#endif
                var targetBuffer = _bufferRings[_readPosition];
                var bytesRead = await _source.ReadAsync(
                    targetBuffer,
                    0,
                    targetBuffer.Length).ConfigureAwait(false);
                _bufferBytesRead[_readPosition] = bytesRead;
#if ENABLE_TRACE
                Console.WriteLine($"PerformReads:  Read into buffer {_readPosition}.");
#endif
                if (bytesRead == 0)
                {
#if ENABLE_TRACE
                    Console.WriteLine($"PerformReads:  Read completed.");
#endif
                    // We are done.
                    _readComplete = true;
                    // We won't actually start reading into the next buffer, but this
                    // frees the buffer we just read into as ready for writing out
                    // to the target.
                    _readPosition = (_readPosition + 1) % _bufferRings.Length;
                    return;
                }

#if ENABLE_STALL_DEBUG
                if ((_readPosition + 1) % _bufferRings.Length == _writePosition)
                {
                    Console.WriteLine($"PerformReads:  [WRITE STALL] Waiting for write position to not equal {_writePosition}.");
                }
#endif
                while ((_readPosition + 1) % _bufferRings.Length == _writePosition)
                {
                    // We can not start filling the next buffer; the write task is still
                    // copying data from it to the output stream.
                    await Task.Yield();
                }

                // We're ready to write into the next read buffer.
#if ENABLE_TRACE
                Console.WriteLine($"PerformReads:  Updating read position.");
#endif
                _readPosition = (_readPosition + 1) % _bufferRings.Length;
            } while (true);
        }

        private async Task PerformWrites()
        {
            // We're waiting for the first read to be completed.
            while (!_readComplete && _writePosition == _readPosition)
            {
#if ENABLE_TRACE
                Console.WriteLine($"PerformWrites: Waiting for read position to not be {_readPosition}.");
#endif
                await Task.Yield();
            }

            do
            {
#if ENABLE_TRACE
                Console.WriteLine($"PerformWrites: Writing from buffer {_writePosition}.");
#endif
                var sourceBuffer = _bufferRings[_writePosition];
                var sourceBufferLength = _bufferBytesRead[_writePosition];
                await _target.WriteAsync(sourceBuffer.AsMemory(0, sourceBufferLength)).ConfigureAwait(false);
#if ENABLE_TRACE
                Console.WriteLine($"PerformWrites: Wrote from buffer {_writePosition}.");
#endif

                if (_hasher != null)
                {
#if ENABLE_TRACE
                    Console.WriteLine($"PerformWrites: Hashing block in {_writePosition}.");
#endif
                    _hasher.TransformBlock(
                        sourceBuffer,
                        0,
                        sourceBufferLength,
                        null,
                        0);
                }

#if ENABLE_STALL_DEBUG
                if (!_readComplete && (_writePosition + 1) % _bufferRings.Length == _readPosition)
                {
                    Console.WriteLine($"PerformWrites: [READ STALL] Waiting for read position to not equal {_readPosition}.");
                }
#endif
                while (!_readComplete && (_writePosition + 1) % _bufferRings.Length == _readPosition)
                {
                    // We can not start reading from the next buffer; the read task is
                    // still filling it with data from the input stream.
                    await Task.Yield();
                }

                if (_readComplete && (_writePosition + 1) % _bufferRings.Length == _readPosition)
                {
                    // The read task didn't actually write into the buffer designated by _readPosition
                    // when the read is complete, so we are done at this point.
#if ENABLE_TRACE
                    Console.WriteLine($"PerformWrites: Finishing writing all data.");
#endif
                    if (_hasher != null)
                    {
                        _hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    }
                    return;
                }

                // We're ready to write from the next source buffer.
#if ENABLE_TRACE
                Console.WriteLine($"PerformWrites: Updating write position.");
#endif
                _writePosition = (_writePosition + 1) % _bufferRings.Length;
            } while (true);
        }
    }
}
