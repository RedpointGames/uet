namespace BuildRunner.BuildGraph.Environment
{
    /// <summary>
    /// Represents settings to then be pssed into a BuildGraphEnvironment later.
    /// </summary>
    internal record BuildGraphSettings
    {
        /// <summary>
        /// All of the -set: parameters to pass to BuildGraph on Windows.
        /// </summary>
        public required Dictionary<string, string> WindowsSettings { get; init; }

        /// <summary>
        /// All of the -set: parameters to pass to BuildGraph on macOS.
        /// </summary>
        public required Dictionary<string, string> MacSettings { get; init; }
    }
}
