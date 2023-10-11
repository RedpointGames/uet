namespace Redpoint.ProcessExecution
{
    /// <summary>
    /// Represents how to start a script.
    /// </summary>
    public class ScriptSpecification : BaseExecutionSpecification
    {
        /// <summary>
        /// The path to the PowerShell script to run.
        /// </summary>
        public required string ScriptPath { get; init; }
    }
}
