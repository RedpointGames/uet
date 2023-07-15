namespace Redpoint.Git.Managed.Packfile
{
    using Redpoint.Numerics;
    using System;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a Git packfile.
    /// </summary>
    public class Packfile : IDisposable
    {
        private static readonly byte[] _header = { (byte)'P', (byte)'A', (byte)'C', (byte)'K' };
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _viewAccessor;
        private readonly uint _version;
        private readonly uint _entryCount;
        private bool _disposed = false;

        /// <summary>
        /// Constructs a new <see cref="Packfile"/> instance by memory mapping the file at the specified path.
        /// </summary>
        /// <param name="path"></param>
        public Packfile(string path)
        {
            _file = MemoryMappedFile.CreateFromFile
                (path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _viewAccessor = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var fileHeaderBytes = new byte[4];
            if (_viewAccessor.ReadArray(0, fileHeaderBytes, 0, fileHeaderBytes.Length) != fileHeaderBytes.Length)
            {
                throw new ArgumentException("The requested file is not long enough to contain a packfile header.", nameof(path));
            }

            _version = EndiannessHelpers.ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(4));
            if (_version != 2)
            {
                throw new NotSupportedException("This packfile is not in a supported version.");
            }

            _entryCount = EndiannessHelpers.ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(8));
        }

        /// <summary>
        /// The number of objects stored in this packfile.
        /// </summary>
        public uint EntryCount => _entryCount;

        /// <summary>
        /// Returns if the given object exists in the packfile and the
        /// associated raw data. If this returns true, you must store the
        /// <paramref name="data"/> and dispose of it once done.
        /// </summary>
        /// <param name="index">The packfile index for this packfile.</param>
        /// <param name="sha">The SHA-1 hash of the object.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="size">The uncompressed size of the object data.</param>
        /// <param name="data">The data stream.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool GetRawPackfileEntry(
            PackfileIndex index,
            UInt160 sha,
            out PackfileEntryObjectType type,
            out ulong size,
            out Stream? data)
        {
            // Locate the object index in the index file based on the SHA-1 hash.
            if (!index.GetObjectIndexForObjectSha(sha, out var objectIndex))
            {
                type = PackfileEntryObjectType.Undefined;
                size = 0;
                data = null;
                return false;
            }

            // Decode the header.
            var offset = (long)index.GetPackfileOffsetForObjectIndex(objectIndex);
            var headerOffset = offset;
            var headerByte = _viewAccessor.ReadByte(headerOffset);
            var hasMoreBits = ((0b10000000u & headerByte) >> 7) == 0b1u;
            type = (PackfileEntryObjectType)((0b01110000u & headerByte) >> 4);
            size = (0b00001111u) & headerByte;
            while (hasMoreBits)
            {
                headerOffset++;
                headerByte = _viewAccessor.ReadByte(headerOffset);
                hasMoreBits = ((0b10000000u & headerByte) >> 7) == 0b1u;
                size = (size << 7) | (0b01111111u & headerByte);
            }

            // Return a stream that wraps decompressing data.
            switch (type)
            {
                case PackfileEntryObjectType.OfsDelta:
                    throw new NotImplementedException();
                case PackfileEntryObjectType.RefDelta:
                    throw new NotImplementedException();
                default:
                    // This is zlib compressed data, but we don't know
                    // the how long the compressed data is. Instead, we make
                    // the underlying stream start from the end of the header
                    // to the end of the packfile, allowing the ZLibStream to
                    // continue accessing bytes until it reaches the final
                    // block of the DEFLATE stream (which it only knows once
                    // the data is decompressed).
                    var compressedStream = _file.CreateViewStream(
                        headerOffset + 1,
                        0,
                        MemoryMappedFileAccess.Read);
                    data = new ZLibStream(
                        compressedStream,
                        CompressionMode.Decompress,
                        leaveOpen: false);
                    break;
            }
            return true;
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
