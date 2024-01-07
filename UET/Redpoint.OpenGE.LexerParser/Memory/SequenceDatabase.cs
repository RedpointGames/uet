namespace Redpoint.OpenGE.LexerParser.Memory
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO.Hashing;
    using System.Numerics;
    using System.Runtime.InteropServices;

    internal class SequenceDatabase<T> where T : struct
    {
        private readonly List<IMemoryOwner<T>> _chunks;
        private readonly List<long> _chunksLength;
        private readonly Dictionary<ulong, ReadOnlyMemory<T>> _index;
        private readonly MemoryPool<T> _pool;
        private Memory<T> _currentMemory;
        private int _currentOffset;

        public SequenceDatabase()
        {
            _chunks = new List<IMemoryOwner<T>>(8);
            _chunksLength = new List<long>(8);
            _index = new Dictionary<ulong, ReadOnlyMemory<T>>(1024);
            _pool = MemoryPool<T>.Shared;
            _currentMemory = default;
            _currentOffset = 0;
        }

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

        public ReadOnlySpan<T> this[ulong hash]
        {
            get
            {
                return _index[hash].Span;
            }
        }
    }
}
