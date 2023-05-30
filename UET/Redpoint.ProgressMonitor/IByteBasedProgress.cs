namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a byte-based progress object that returns the position and length to report on.
    /// </summary>
    public interface IByteBasedProgress
    {
        /// <summary>
        /// The current position within the byte-based operation.
        /// </summary>
        long Position { get; }

        /// <summary>
        /// The total length of the byte-based operation.
        /// </summary>
        long Length { get; }
    }
}