namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a monitor which observes a Git fetch operation and computes progress messages.
    /// </summary>
    public interface IGitFetchBasedMonitor : IEmbeddableMonitor<IGitFetchBasedProgress>
    {
    }
}