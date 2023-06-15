namespace Redpoint.ProcessTree
{
    using System.Diagnostics;

    /// <summary>
    /// Provides access to the process tree.
    /// </summary>
    public interface IProcessTree
    {
        /// <summary>
        /// Get the parent process of the current process, or null if this process does not have a parent process, or if the parent process could not be accessed.
        /// </summary>
        /// <returns>The parent process of the current process.</returns>
        Process? GetParentProcess();

        /// <summary>
        /// Gets the parent process of a process by ID, or null if the target process does not have a parent process, or if the parent process could not be accessed.
        /// </summary>
        /// <param name="id">The ID of the process to get the parent of.</param>
        /// <returns>The parent process of the target process.</returns>
        Process? GetParentProcess(int id);

        /// <summary>
        /// Gets the parent process of another process, or null if the target process does not have a parent process, or if the parent process could not be accessed.
        /// </summary>
        /// <param name="process">The process to get the parent of.</param>
        /// <returns>The parent process of the target process.</returns>
        Process? GetParentProcess(Process process);
    }
}