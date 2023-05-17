namespace Redpoint.UET.BuildPipeline.Environment
{
    using System.Collections.Generic;

    /// <summary>
    /// WindowsEnvironment contains the Windows-specific environment
    /// settings for jobs that run on Windows.
    /// </summary>
    public record BuildGraphWindowsEnvironment
    {
        /// <summary>
        /// The absolute path to shared storage on Windows. Must start with a drive letter (like X:\). Must have a trailing slash.
        /// </summary>
        public required string? SharedStorageAbsolutePath { get; init; }
    }
}
