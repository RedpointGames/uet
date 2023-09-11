namespace Redpoint.OpenGE.Core
{
    using System;

    public delegate void SequentialVersion1DecoderReadHeader(long blobHashXx64, long byteLength);
    public delegate void SequentialVersion1DecoderReadData(long blobHashXx64, long blobOutputOffset, byte[] buffer, int bufferOffset, int bufferCount);

    /// <summary>
    /// Decodes a binary byte stream known as "Sequential Version 1" format into a bunch of blobs, invoking the callbacks passed in the construct as the stream is written to. In actual transfers, this is decompressed from Brotli compression, where the original stream is known as the "Brotli Sequential Version 1" format.
    /// </summary>
    public class SequentialVersion1DecoderStream : Stream
    {
        private readonly SequentialVersion1DecoderReadHeader _onHeaderRead;
        private readonly SequentialVersion1DecoderReadData _onDataRead;
        private long _position;
        private bool _isReadingHeader;
        private long _remainingBytesInHeader;
        private long _remainingBytesInData;
        private long _currentOffsetInData;
        private long _currentHash;
        private byte[] _headerBuffer;

        public SequentialVersion1DecoderStream(
            SequentialVersion1DecoderReadHeader onHeaderRead,
            SequentialVersion1DecoderReadData onDataRead)
        {
            _onHeaderRead = onHeaderRead;
            _onDataRead = onDataRead;
            _position = 0;
            _isReadingHeader = true;
            _remainingBytesInHeader = sizeof(long) * 2;
            _remainingBytesInData = 0;
            _currentOffsetInData = 0;
            _currentHash = 0;
            _headerBuffer = new byte[sizeof(long) * 2];
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => _position; set => _position = value; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            while (count > 0)
            {
                if (_isReadingHeader)
                {
                    var bytesToFillHeader = (int)Math.Min(
                        _remainingBytesInHeader,
                        count);
                    Array.Copy(
                        buffer,
                        offset,
                        _headerBuffer,
                        _headerBuffer.Length - _remainingBytesInHeader,
                        bytesToFillHeader);
                    _remainingBytesInHeader -= bytesToFillHeader;
                    offset += bytesToFillHeader;
                    count -= bytesToFillHeader;
                    if (count == 0)
                    {
                        return;
                    }
                    else if (_remainingBytesInData == 0)
                    {
                        _isReadingHeader = false;
                        _currentHash = BitConverter.ToInt64(
                            _headerBuffer,
                            0);
                        _remainingBytesInData = BitConverter.ToInt64(
                            _headerBuffer,
                            sizeof(long));
                        _currentOffsetInData = 0;
                        _onHeaderRead(_currentHash, _remainingBytesInData);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                if (!_isReadingHeader)
                {
                    var bytesToReadData = (int)Math.Min(
                        _remainingBytesInData,
                        count);
                    _onDataRead(
                        _currentHash,
                        _currentOffsetInData,
                        buffer,
                        offset,
                        bytesToReadData);
                    _currentOffsetInData += bytesToReadData;
                    _remainingBytesInData -= bytesToReadData;
                    offset += bytesToReadData;
                    count -= bytesToReadData;
                    if (_remainingBytesInData < 0)
                    {
                        throw new InvalidOperationException();
                    }
                    else if (_remainingBytesInData == 0)
                    {
                        _isReadingHeader = true;
                        _remainingBytesInHeader = sizeof(long) * 2;
                    }
                }
            }
        }
    }
}