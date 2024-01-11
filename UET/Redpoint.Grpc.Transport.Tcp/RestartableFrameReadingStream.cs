namespace Redpoint.Grpc.Transport.Framing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    // - create one instance of RestartableFrameReadingStream and
    //   RestartableFrameWritingStream for the connection
    // - then call SetCurrentFrameInfo once we have a new frame
    // - (might need a callback on the reader to see whether there is
    //   new data to get the frame header)
    // - then connection class should implement framing on top of a
    //   Stream, including serialization/deserialization
    // - on top of frames, we then need to implement call headers (i.e. what method are we calling, streaming, results, etc.)
    // - then .Tcp library implements using the connection class with a TCP server/client socket

    // Type.Parser.ParseFrom
    // can use generic for generic read here

    /// <remarks>
    /// This stream doesn't implement asynchronous reading since Protobuf for C# doesn't support using this async methods during parsing.
    /// </remarks>
    internal class RestartableFrameReadingStream : Stream
    {
        private readonly NetworkStream _underlyingStream;
        private FrameInfo _currentFrameInfo;
        private long _currentPosition;

        public RestartableFrameReadingStream(
            NetworkStream underlyingStream)
        {
            _underlyingStream = underlyingStream;
            _currentFrameInfo = default;
            _currentPosition = 0L;
        }

        public void SetCurrentFrameInfo(FrameInfo frameInfo)
        {
            _currentFrameInfo = frameInfo;
            _currentPosition = 0;
        }

        public bool DataAvailable
        {
            get => _underlyingStream.DataAvailable;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _currentFrameInfo.Length;

        public override long Position
        {
            get => _currentPosition;
            set => throw new NotSupportedException("This stream does not support seeking.");
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remainingDataInFrame = _currentFrameInfo.Length - _currentPosition;
            var bytesToRead = Math.Min(count, remainingDataInFrame);
            var bytesRead = _underlyingStream.Read(buffer, offset, (int)bytesToRead);
            _currentPosition += bytesRead;
            return bytesRead;
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
