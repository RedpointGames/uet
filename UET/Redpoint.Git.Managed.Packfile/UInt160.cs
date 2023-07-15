namespace Redpoint.Git.Managed.Packfile
{
    using System.Buffers.Binary;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics;

    /// <summary>
    /// Represents a 160-bit unsigned integer, which can store SHA1 hashes and efficiently compare them.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct UInt160 :
        IComparable<UInt160>, 
        IEqualityOperators<UInt160, UInt160, bool>,
        IEqualityComparer<UInt160>,
        IEquatable<UInt160>
    {
        [FieldOffset(0)]
        private fixed byte _bytes[20];

        [FieldOffset(0)]
        private Vector128<ulong> _lower;

        [FieldOffset(0)]
        private ulong _lowerLow;
        [FieldOffset(8)]
        private ulong _lowerHigh;
        [FieldOffset(16)]
        private uint _upper;

        [FieldOffset(0)]
        private uint _0;
        [FieldOffset(4)]
        private uint _1;
        [FieldOffset(8)]
        private uint _2;
        [FieldOffset(12)]
        private uint _3;
        [FieldOffset(16)]
        private uint _4;

        /// <summary>
        /// Returns the most significant byte; that is, the byte that
        /// would appear first in a string representation of the
        /// SHA1 hash.
        /// </summary>
        public readonly byte MostSignificantByte =>
            BitConverter.IsLittleEndian ? _bytes[0] : _bytes[19];

        /// <summary>
        /// Returns the byte of the unsigned 160-bit integer as if the
        /// integer is stored in little endian format (regardless of the
        /// current CPU architecture. Therefore <c>[0]</c> is always the
        /// most significant byte.
        /// </summary>
        /// <param name="index">The index from 0 to 19 inclusive.</param>
        /// <returns>The byte of the unsigned 160-bit integer.</returns>
        public readonly byte this[byte index] => BitConverter.IsLittleEndian ? _bytes[index] : _bytes[19 - index];

        /// <summary>
        /// Create an unsigned 160-bit integer where from 20 bytes of unsafe memory, where
        /// the most significant byte is the last byte.
        /// </summary>
        /// <param name="b">20 bytes of unsafe memory.</param>
        /// <returns>The unsigned 160-bit integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UInt160 CreateFromBigEndian(byte* b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return CreateFromOppositeEndian(b);
            }
            else
            {
                return CreateFromMatchingEndian(b);
            }
        }

        /// <summary>
        /// Create an unsigned 160-bit integer where from 20 bytes of unsafe memory, where
        /// the most significant byte is the first byte.
        /// </summary>
        /// <param name="b">20 bytes of unsafe memory.</param>
        /// <returns>The unsigned 160-bit integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UInt160 CreateFromLittleEndian(byte* b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return CreateFromMatchingEndian(b);
            }
            else
            {
                return CreateFromOppositeEndian(b);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe UInt160 CreateFromMatchingEndian(byte* b)
        {
            return *(UInt160*)b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe UInt160 CreateFromOppositeEndian(byte* b)
        {
            var be = (UInt160*)b;
            UInt160 h = default;
            h._4 = BinaryPrimitives.ReverseEndianness(be->_0);
            h._3 = BinaryPrimitives.ReverseEndianness(be->_1);
            h._2 = BinaryPrimitives.ReverseEndianness(be->_2);
            h._1 = BinaryPrimitives.ReverseEndianness(be->_3);
            h._0 = BinaryPrimitives.ReverseEndianness(be->_4);
            return h;
        }

        /// <summary>
        /// Parses a SHA1 hash from a hexadecimal string value (regardless of casing).
        /// </summary>
        /// <param name="hash">A hexadecimal string value like 'd6340facfbb763c6d516e7599eac94245fce52ec'.</param>
        /// <exception cref="ArgumentException">Thrown if the string hash is not exactly 40 characters in length.</exception>
        /// <returns></returns>
        public static UInt160 CreateFromString(string hash)
        {
            var bytes = Convert.FromHexString(hash);
            if (bytes.Length != 20)
            {
                throw new ArgumentException("Hexadecimal SHA1 hash must be exactly 40 characters long.", nameof(hash));
            }
            fixed (byte* b = bytes)
            {
                return CreateFromLittleEndian(b);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            fixed (byte* b = _bytes)
            {
                var reversed = CreateFromLittleEndian(b);
                return Convert.ToHexString(new ReadOnlySpan<byte>(reversed._bytes, 20)).ToLowerInvariant();
            }
        }

        /// <inheritdoc />
        public unsafe int CompareTo(UInt160 right)
        {
            if (_upper < right._upper)
            {
                return -1;
            }
            else if (_upper > right._upper)
            {
                return 1;
            }
            else
            {
                if (_lowerHigh < right._lowerHigh)
                {
                    return -1;
                }
                else if (_lowerHigh > right._lowerHigh)
                {
                    return 1;
                }
                else
                {
                    if (_lowerLow < right._lowerLow)
                    {
                        return -1;
                    }
                    else if (_lowerLow > right._lowerLow)
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UInt160 x, UInt160 y)
        {
            return x == y;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UInt160 other)
        {
            return this == other;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int GetHashCode([DisallowNull] UInt160 obj)
        {
            return unchecked(_upper.GetHashCode() * 17 + _lower.GetHashCode());
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is UInt160 entry)
            {
                return this == entry;
            }

            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator ==(UInt160 left, UInt160 right)
        {
            return left._upper == right._upper && 
                Vector128.EqualsAll(left._lower, right._lower);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator !=(UInt160 left, UInt160 right)
        {
            return left._upper != right._upper || 
                !Vector128.EqualsAll(left._lower, right._lower);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator <(UInt160 left, UInt160 right)
        {
            return left._upper < right._upper ||
                (left._upper == right._upper && 
                (left._lowerHigh < right._lowerHigh || 
                (left._lowerHigh == right._lowerHigh && 
                left._lowerLow < right._lowerLow)));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator <=(UInt160 left, UInt160 right)
        {
            return left._upper < right._upper ||
                (left._upper == right._upper &&
                (left._lowerHigh < right._lowerHigh ||
                (left._lowerHigh == right._lowerHigh &&
                left._lowerLow <= right._lowerLow)));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator >(UInt160 left, UInt160 right)
        {
            return left._upper > right._upper ||
                (left._upper == right._upper &&
                (left._lowerHigh > right._lowerHigh ||
                (left._lowerHigh == right._lowerHigh &&
                left._lowerLow > right._lowerLow)));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool operator >=(UInt160 left, UInt160 right)
        {
            return left._upper > right._upper ||
                (left._upper == right._upper &&
                (left._lowerHigh > right._lowerHigh ||
                (left._lowerHigh == right._lowerHigh &&
                left._lowerLow >= right._lowerLow)));
        }
    }
}