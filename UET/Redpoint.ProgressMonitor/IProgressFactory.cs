namespace Redpoint.ProgressMonitor
{
    using System;
    using System.IO;

    /// <summary>
    /// Factory interface for creating progress objects.
    /// </summary>
    public interface IProgressFactory
    {
        /// <summary>
        /// Creates a byte-based progress object which reports on the position and length of a stream.
        /// </summary>
        /// <param name="stream">The stream to report the progress of.</param>
        /// <returns>The new progress object.</returns>
        IByteBasedProgress CreateProgressForStream(Stream stream);

        /// <summary>
        /// Creates a byte-based progress object which reports on the position and length of a chunked stream.
        /// </summary>
        /// <param name="chunkedStreamProgress">The chunked stream to report the progress of.</param>
        /// <param name="totalLength">The total length of all chunks combined.</param>
        /// <returns>The new progress object.</returns>
        IByteBasedProgress CreateProgressForStream(ChunkedStreamProgress chunkedStreamProgress, long totalLength);

        /// <summary>
        /// Creates a byte-based progress object which reports on position and length using callbacks.
        /// </summary>
        /// <param name="getPosition">Called to receive the current position.</param>
        /// <param name="getLength">Called to receive the current length.</param>
        /// <returns>The new progress object.</returns>
        IByteBasedProgress CreateProgressForCallback(Func<long> getPosition, Func<long> getLength);
    }
}
