namespace Redpoint.UET.BuildGraph.Environment
{
    using System.Collections.Generic;

    /// <summary>
    /// MacEnvironment contains the macOS-specific environment
    /// settings for jobs that run on macOS.
    /// </summary>
    public record BuildGraphMacEnvironment
    {
        /// <summary>
        /// All of the -set: parameters to pass to BuildGraph on macOS.
        /// </summary>
        public required Dictionary<string, string> BuildGraphSettings { get; init; }

        /// <summary>
        /// The engine path to use on macOS agents. This is used if you're targeting a custom engine where the paths different on Windows and macOS. You don't need to set it if you're just using a version number like "4.27" with the -Engine parameter.
        /// </summary>
        public required string? MacEnginePathOverride { get; init; }

        /// <summary>
        /// The absolute path to shared storage on macOS. This will not be set if the artifact transport is not Direct.
        /// </summary>
        public required string? SharedStorageAbsolutePath { get; init; }
    }
}
