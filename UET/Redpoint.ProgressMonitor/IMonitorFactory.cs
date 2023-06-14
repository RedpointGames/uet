namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Factory interface for creating monitors.
    /// </summary>
    public interface IMonitorFactory
    {
        /// <summary>
        /// Creates a monitor for byte-based progress.
        /// </summary>
        /// <returns>The new monitor.</returns>
        IByteBasedMonitor CreateByteBasedMonitor();

        /// <summary>
        /// Creates a monitor for a Git fetch operation.
        /// </summary>
        /// <returns>The new monitor.</returns>
        IGitFetchBasedMonitor CreateGitFetchBasedMonitor();

        /// <summary>
        /// Creates a monitor for a task based operation.
        /// </summary>
        /// <returns>The new monitor.</returns>
        ITaskBasedMonitor CreateTaskBasedMonitor();
    }
}
