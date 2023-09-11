namespace Redpoint.ProcessExecution
{
    using System.Runtime.Versioning;

    public class ProcessSpecification : BaseExecutionSpecification
    {
        public required string FilePath { get; set; }

        [SupportedOSPlatform("windows")]
        public IReadOnlyDictionary<char, string>? PerProcessDriveMappings { get; set; }
    }
}
