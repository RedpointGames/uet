namespace Redpoint.ProgressMonitor
{
    internal class StreamByteBasedProgress : IByteBasedProgress
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
