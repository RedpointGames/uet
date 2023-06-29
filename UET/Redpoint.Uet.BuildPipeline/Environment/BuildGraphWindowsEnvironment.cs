namespace Redpoint.Uet.BuildPipeline.Environment
{
    /// <summary>
    /// WindowsEnvironment contains the Windows-specific environment
    /// settings for jobs that run on Windows.
    /// </summary>
    public record BuildGraphWindowsEnvironment
    {
        /// <summary>
        /// The absolute path to shared storage on Windows. Must start with a drive letter (like X:\). Must have a trailing slash.
        /// </summary>
        public required string SharedStorageAbsolutePath { get; init; }

        /// <summary>
        /// The absolute path to SDKs installed on Windows. If this is set, UET will automatically install and manage platform SDKs
        /// in this location for you.
        /// </summary>
        public required string? SdksPath { get; init; }
    }
}
