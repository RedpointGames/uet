namespace Redpoint.ProgressMonitor.Implementations
{
    using System.IO;

    internal sealed class StreamByteBasedProgress : IByteBasedProgress
    {
        private readonly Stream _stream;

        public StreamByteBasedProgress(Stream stream)
        {
            _stream = stream;
        }

        public long Position => _stream.Position;

        public long Length => _stream.Length;
    }
}
