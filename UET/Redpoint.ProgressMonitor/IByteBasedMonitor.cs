namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a monitor which observes a byte position within a length and computes progress messages.
    /// </summary>
    public interface IByteBasedMonitor : IEmbeddableMonitor<IByteBasedProgress>
    {
    }
}