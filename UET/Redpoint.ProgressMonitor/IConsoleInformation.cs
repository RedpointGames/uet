namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents information about the current console. Rather than the library directly querying console information, users implement this interface so that the console width can be ignored (for e.g. CI and logging scenarios).
    /// </summary>
    public interface IConsoleInformation
    {
        /// <summary>
        /// The width of the console. If this is set, the monitor may truncate or reduce the information reported so that it will fit on a single line.
        /// </summary>
        int? Width { get; }
    }
}