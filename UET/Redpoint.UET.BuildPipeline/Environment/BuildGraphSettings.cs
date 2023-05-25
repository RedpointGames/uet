namespace Redpoint.UET.BuildPipeline.Environment
{
    /// <summary>
    /// Represents settings to then be passed into a BuildGraphEnvironment later.
    /// </summary>
    public record BuildGraphSettings
    {
        /// <summary>
        /// All of the -set: parameters to pass to BuildGraph on Windows.
        /// </summary>
        public required Dictionary<string, string> WindowsSettings { get; init; }

        /// <summary>
        /// All of the -set: parameters to pass to BuildGraph on macOS.
        /// </summary>
        public Dictionary<string, string>? MacSettings { get; init; } = null;
    }
}
