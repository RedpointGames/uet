namespace Redpoint.Uet.BuildPipeline.Environment
{
    /// <summary>
    /// MacEnvironment contains the macOS-specific environment
    /// settings for jobs that run on macOS.
    /// </summary>
    public record BuildGraphMacEnvironment
    {
        /// <summary>
        /// The absolute path to shared storage on macOS. This will not be set if the artifact transport is not Direct.
        /// </summary>
        public required string SharedStorageAbsolutePath { get; init; }

        /// <summary>
        /// The absolute path to SDKs installed on macOS. If this is set, UET will automatically install and manage platform SDKs
        /// in this location for you.
        /// </summary>
        public required string? SdksPath { get; init; }

        /// <summary>
        /// The absolute path that Unreal build tools should output telemetry data to as OpenTracing JSON files. If this is set, 
        /// UET will pass this directory to BuildGraph by setting the UE_TELEMETRY_DIR on macOS build agents.
        /// </summary>
        public required string? TelemetryPath { get; init; }
    }
}
