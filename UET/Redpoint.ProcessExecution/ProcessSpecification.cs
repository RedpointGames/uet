namespace Redpoint.ProcessExecution
{
    using System.Runtime.Versioning;

    /// <summary>
    /// Represents how to start a process.
    /// </summary>
    public class ProcessSpecification : BaseExecutionSpecification
    {
        /// <summary>
        /// The path to the executable binary to run.
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// On Windows, specifies how drive mappings should be overridden on a per-process basis. You can use this as a lightweight sandboxing solution, but please note that it is trivial to escape from such a sandbox, so it is best suited to virtualising an environment where processes on different machines need to see the same filesystem layout.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public IReadOnlyDictionary<char, string>? PerProcessDriveMappings { get; set; }
    }
}
