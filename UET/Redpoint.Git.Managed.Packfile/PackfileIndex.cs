namespace Redpoint.Git.Managed.Packfile
{
    using System.Buffers.Binary;
    using System.IO.MemoryMappedFiles;

    /// <summary>
    /// Represents a Git packfile index.
    /// </summary>
    public class PackfileIndex : IDisposable
    {
        private static readonly byte[] _header = new byte[] { 0xFF, 0x74, 0x4F, 0x63, 0x00, 0x00, 0x00, 0x02 };
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _viewAccessor;
        private readonly uint _objectCount;
        private bool _disposed = false;

        /// <summary>
        /// Construct a new Git packfile index by memory mapping the file at the specified path.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the specified file is not a packfile index.</exception>
        public PackfileIndex(string path)
        {
            _file = MemoryMappedFile.CreateFromFile
                (path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _viewAccessor = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var fileHeaderBytes = new byte[8];
            if (_viewAccessor.ReadArray(0, fileHeaderBytes, 0, fileHeaderBytes.Length) != fileHeaderBytes.Length)
            {
                throw new ArgumentException("The requested file is not long enough to contain a packfile index header.", nameof(path));
            }
            if (!fileHeaderBytes.SequenceEqual(_header))
            {
                throw new ArgumentException("The requested file did not contain the expected packfile index header.", nameof(path));
            }

            _objectCount = LowLevelGetObjectIndexAtFanoutIndex((byte)0xFFu);
        }

        /// <summary>
        /// The number of objects in the packfile index.
        /// </summary>
        public uint ObjectCount => _objectCount;

        /// <summary>
        /// Gets the object index stored at the fanout table index.
        /// </summary>
        /// <param name="index">The fanout table index.</param>
        /// <returns>The object index in the fanout table.</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public uint LowLevelGetObjectIndexAtFanoutIndex(byte index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            var offset = _header.Length + index * sizeof(int);
            return ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(offset));
        }

        /// <summary>
        /// Returns the SHA1 value at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The SHA1 value.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public UInt160 LowLevelGetShaAtObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + index * 20;
            _viewAccessor.Read<UInt160>(offset, out var entry);
            return entry;
        }

        /// <summary>
        /// Returns the CRC checksum at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The CRC checksum.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public uint LowLevelGetCrcAtObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + _objectCount * 20 + index * sizeof(uint);
            return _viewAccessor.ReadUInt32(offset);
        }

        /// <summary>
        /// Returns the offset at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The offset in the main packfile of this object.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public ulong LowLevelGetOffsetAtObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + _objectCount * 20 + _objectCount * sizeof(uint) + index * sizeof(uint);
            var offsetInPackfile = ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(offset));

            if ((offsetInPackfile & 1u << 31) != 0)
            {
                // This refers to an index in the 64-byte offset table.
                var offsetInLargeIndex = offsetInPackfile & ~(1u << 31);
                var largeIndexOffsetInIndex = _header.Length + 256 * sizeof(int) + _objectCount * 20 + _objectCount * sizeof(uint) + _objectCount * sizeof(uint) + offsetInLargeIndex * sizeof(ulong);
                var v = _viewAccessor.ReadUInt64(largeIndexOffsetInIndex);
                return ConvertFromNetworkByteOrder(v);
            }
            else
            {
                return offsetInPackfile;
            }
        }

        private uint ConvertFromNetworkByteOrder(uint v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }

        private ulong ConvertFromNetworkByteOrder(ulong v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            _disposed = true;
            _viewAccessor.Dispose();
            _file.Dispose();
        }
    }
}