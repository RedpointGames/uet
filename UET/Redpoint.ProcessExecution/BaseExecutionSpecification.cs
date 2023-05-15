namespace Redpoint.ProcessExecution
{
    using System.Collections.Generic;

    public class BaseExecutionSpecification
    {
        public required IEnumerable<string> Arguments { get; init; }

        public Dictionary<string, string>? EnvironmentVariables { get; init; }

        public string? WorkingDirectory { get; init; }

        public string? StdinData { get; init; }
    }
}
