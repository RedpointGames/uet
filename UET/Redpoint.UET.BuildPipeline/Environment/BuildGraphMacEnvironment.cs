namespace Redpoint.UET.BuildPipeline.Environment
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
    }
}
