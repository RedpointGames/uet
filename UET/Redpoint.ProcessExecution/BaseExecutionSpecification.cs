namespace Redpoint.ProcessExecution
{
    using System.Collections.Generic;

    public class BaseExecutionSpecification
    {
        public required IEnumerable<string> Arguments { get; init; }

        public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; set; }

        public string? WorkingDirectory { get; init; }

        public string? StdinData { get; init; }
    }
}
