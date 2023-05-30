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
    }
}
