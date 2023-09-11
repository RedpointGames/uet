namespace Redpoint.ProgressMonitor.Implementations
{
    internal sealed class ChunkedStreamByteBasedProgress : IByteBasedProgress
    {
        private ChunkedStreamProgress _chunkedStreamProgress;

        public ChunkedStreamByteBasedProgress(ChunkedStreamProgress chunkedStreamProgress, long totalLength)
        {
            _chunkedStreamProgress = chunkedStreamProgress;
            Length = totalLength;
        }

        public long Position
        {
            get
            {
                return _chunkedStreamProgress.ChunkOffset + (_chunkedStreamProgress.ChunkStream?.Position ?? 0);
            }
        }

        public long Length { get; }
    }
}