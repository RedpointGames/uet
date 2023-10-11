namespace Redpoint.ProcessExecution
{
    using System.Collections.Generic;

    /// <summary>
    /// The specification properties shared between <see cref="ProcessSpecification"/> and <see cref="ScriptSpecification"/>.
    /// </summary>
    public class BaseExecutionSpecification
    {
        /// <summary>
        /// The arguments to pass to the process or script.
        /// </summary>
        public required IEnumerable<string> Arguments { get; init; }

        /// <summary>
        /// The environment variables to override for the process or script. If null, the environment variables are inherited from the current process.
        /// </summary>
        public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>
        /// The working directory to run the process or script in. If null, the process or script inherits the current working directory.
        /// </summary>
        public string? WorkingDirectory { get; init; }

        /// <summary>
        /// The standard input data to pass to the process when it runs. Standard input can be provided to a process or script via this property or via <see cref="ICaptureSpecification.OnRequestStandardInputAtStartup"/>. Both mechanisms will cause the process's standard input stream to be redirected if either of them are used.
        /// </summary>
        public string? StdinData { get; init; }
    }
}
