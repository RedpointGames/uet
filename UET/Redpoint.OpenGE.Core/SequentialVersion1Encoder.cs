namespace Redpoint.OpenGE.Core
{
    using System.Text;

    /// <summary>
    /// Encodes a bunch of blobs (indexed in a dictionary, but only the 
    /// missing blobs are transferred) into a binary byte stream known as 
    /// "Sequential Version 1" format. In actual transfers, this is then 
    /// compressed with Brotli compression to result in the "Brotli Sequential 
    /// Version 1" format.
    /// </summary>
    public class SequentialVersion1Encoder : Stream
    {
        private readonly IReadOnlyDictionary<long, BlobInfo> _allEntriesByBlobHash;
        private readonly long[] _orderedMissingBlobHashes;
        private readonly long _length;
        private long _position;
        private const long _entryHeader = sizeof(long) + sizeof(long);
        private int _currentEntryIndex;
        private int _currentEntryPosition;

        public SequentialVersion1Encoder(
            IReadOnlyDictionary<long, BlobInfo> allEntriesByBlobHash,
            IEnumerable<long> missingBlobHashes)
        {
            if (allEntriesByBlobHash.Count == 0)
            {
                throw new ArgumentException("Must have at least one blob", nameof(allEntriesByBlobHash));
            }
            _allEntriesByBlobHash = allEntriesByBlobHash;
            _orderedMissingBlobHashes = missingBlobHashes.OrderBy(x => x).ToArray();
            _length = _allEntriesByBlobHash.Values.Sum(x => x.ByteLength + _entryHeader);
            _position = 0;
            _currentEntryIndex = 0;
            _currentEntryPosition = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _position; set => _position = value; }

        public override void Flush()
        {
            return;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var written = 0;
        readNextFile:
            if (_currentEntryIndex == _orderedMissingBlobHashes.Length)
            {
                return written;
            }
            var blobHash = _orderedMissingBlobHashes[_currentEntryIndex];
            var blobEntry = _allEntriesByBlobHash[blobHash];
            if (_currentEntryPosition < _entryHeader)
            {
                // Generate the header.
                var header = new byte[_entryHeader];
                Array.Copy(
                    BitConverter.GetBytes(blobHash),
                    0,
                    header,
                    0,
                    sizeof(long));
                Array.Copy(
                    BitConverter.GetBytes(blobEntry.ByteLength),
                    0,
                    header,
                    sizeof(long),
                    sizeof(long));

                // Copy the header into the buffer.
                var headerCount = Math.Min(
                    count,
                    header.Length - _currentEntryPosition);
                Array.Copy(
                    header,
                    _currentEntryPosition,
                    buffer,
                    offset,
                    headerCount);
                offset += headerCount;
                _currentEntryPosition += headerCount;
                _position += headerCount;
                written += headerCount;
                count -= headerCount;
            }
            if (count < 0)
            {
                throw new InvalidOperationException();
            }
            if (count == 0)
            {
                // We've finished writing.
                return written;
            }
            if (blobEntry.Path != null)
            {
                using (var stream = new FileStream(blobEntry.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    stream.Seek(_currentEntryPosition - _entryHeader, SeekOrigin.Begin);
                    var bytesRead = stream.Read(buffer, offset, count);
                    offset += bytesRead;
                    _currentEntryPosition += bytesRead;
                    _position += bytesRead;
                    written += bytesRead;
                    count -= bytesRead;
                }
            }
            else
            {
                var sourceBuffer = Encoding.UTF8.GetBytes(blobEntry.Content!);
                var bytesRead = (int)Math.Min(sourceBuffer.Length - (_currentEntryPosition - _entryHeader), count);
                Array.Copy(
                    sourceBuffer,
                    _currentEntryPosition - _entryHeader,
                    buffer,
                    offset,
                    bytesRead);
                offset += bytesRead;
                _currentEntryPosition += bytesRead;
                _position += bytesRead;
                written += bytesRead;
                count -= bytesRead;
            }
            if (count < 0 || _currentEntryPosition > blobEntry.ByteLength + _entryHeader)
            {
                throw new InvalidOperationException();
            }
            if (_currentEntryPosition == blobEntry.ByteLength + _entryHeader)
            {
                _currentEntryIndex++;
                _currentEntryPosition = 0;
                // @note: This will return written if we've processed
                // the last entry.
                goto readNextFile;
            }
            else if (count == 0)
            {
                // We've finished writing.
                return written;
            }
            else
            {
                throw new InvalidOperationException();
            }
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
    }
}
