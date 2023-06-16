namespace Redpoint.ProgressMonitor
{
    using System.IO;

    /// <summary>
    /// Represents the progress of a chunked operation, where the operation is split over multiple streams,
    /// each at a different offset.
    /// </summary>
    public class ChunkedStreamProgress
    {
        /// <summary>
        /// The offset of the current chunk being processed.
        /// </summary>
        public long ChunkOffset { get; set; }

        /// <summary>
        /// The current chunk's stream that is being processed.
        /// </summary>
        public Stream? ChunkStream { get; set; }
    }
}
