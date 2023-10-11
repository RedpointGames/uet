namespace Redpoint.PathResolution
{
    /// <summary>
    /// Provides an interface which resolves executable binary names to absolute paths via the PATH environment variable.
    /// </summary>
    public interface IPathResolver
    {
        /// <summary>
        /// Resolve the specified executable name to an absolute path if the executable binary exists on the current PATH.
        /// </summary>
        /// <param name="binaryName">The name of the executable binary, such as 'git'.</param>
        /// <returns>The absolute path to the executable binary.</returns>
        /// <exception cref="FileNotFoundException">Thrown if no such executable binary exists on the current PATH.</exception>
        Task<string> ResolveBinaryPath(string binaryName);
    }
}