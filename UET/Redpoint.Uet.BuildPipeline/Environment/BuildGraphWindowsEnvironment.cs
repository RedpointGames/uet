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

        /// <summary>
        /// The absolute path that Unreal build tools should output telemetry data to as OpenTracing JSON files. If this is set, 
        /// UET will pass this directory to BuildGraph by setting the UE_TELEMETRY_DIR on Windows build agents.
        /// </summary>
        public required string? TelemetryPath { get; init; }
    }
}
