namespace Redpoint.CppPreprocessor.Memory
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO.Hashing;
    using System.Numerics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// An efficient database for hashing and storing sequences, such that each unique sequence is only stored once.
    /// </summary>
    /// <typeparam name="T">The type to store in the database, typically <see cref="char"/>.</typeparam>
    public class SequenceDatabase<T> where T : struct
    {
        private readonly List<IMemoryOwner<T>> _chunks;
        private readonly List<long> _chunksLength;
        private readonly Dictionary<ulong, ReadOnlyMemory<T>> _index;
        private readonly MemoryPool<T> _pool;
        private Memory<T> _currentMemory;
        private int _currentOffset;

        /// <summary>
        /// Construct a new sequence database.
        /// </summary>
        public SequenceDatabase()
        {
            _chunks = new List<IMemoryOwner<T>>(8);
            _chunksLength = new List<long>(8);
            _index = new Dictionary<ulong, ReadOnlyMemory<T>>(1024);
            _pool = MemoryPool<T>.Shared;
            _currentMemory = default;
            _currentOffset = 0;
        }

        /// <summary>
        /// Store a sequence in the database if it does not already exist, and return the hash of the value.
        /// </summary>
        /// <param name="sequence">The span representing the sequence.</param>
        /// <returns>The hash value.</returns>
        public ulong Store(ReadOnlySpan<T> sequence)
        {
            var hash = XxHash3.HashToUInt64(MemoryMarshal.AsBytes(sequence));
            if (_index.ContainsKey(hash))
            {
                return hash;
            }
            if (sequence.Length > _currentMemory.Length - _currentOffset)
            {
                if (!_currentMemory.IsEmpty)
                {
                    _chunksLength.Add(_currentOffset);
                }
                var desiredLength = Math.Min(2048, _currentMemory.Length);
                if (sequence.Length > desiredLength)
                {
                    desiredLength = (int)BitOperations.RoundUpToPowerOf2((uint)sequence.Length);
                }
                var newMemory = _pool.Rent(desiredLength * 2);
                _chunks.Add(newMemory);
                _currentMemory = newMemory.Memory;
                _currentOffset = 0;
                _index.EnsureCapacity(
                    (int)BitOperations.RoundUpToPowerOf2((uint)_index.Count));
            }
            var slice = _currentMemory.Slice(_currentOffset, sequence.Length);
            sequence.CopyTo(slice.Span);
            _currentOffset += sequence.Length;
            _index.Add(hash, slice);
            return hash;
        }

        /// <summary>
        /// Retrieve a read-only sequence based on the hash previously returned by <see cref="Store(ReadOnlySpan{T})"/>.
        /// </summary>
        /// <param name="hash">The hash value.</param>
        /// <returns>The read-only sequence representing the original value.</returns>
        public ReadOnlySpan<T> this[ulong hash]
        {
            get
            {
                return _index[hash].Span;
            }
        }
    }
}
