namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a monitor which observes multiple tasks (stages) and computes progress messages.
    /// </summary>
    public interface ITaskBasedMonitor : IEmbeddableMonitor<ITaskBasedProgress>
    {
    }
}