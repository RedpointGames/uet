namespace Redpoint.CommandLine
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains all information about the command to execute, for <see cref="CommandExecutionHandler"/>.
    /// </summary>
    public class CommandExecution
    {
        /// <summary>
        /// The runtime service provider used for executing the command.
        /// </summary>
        public required IServiceProvider ServiceProvider { get; init; }

        /// <summary>
        /// The delegate to invoke when the command should be executed.
        /// </summary>
        public required Func<Task<int>> ExecuteCommandAsync { get; init; }

        /// <summary>
        /// The command that will be executed.
        /// </summary>
        public required Command Command { get; init; }

        /// <summary>
        /// The command line parse result.
        /// </summary>
        public required ICommandInvocationContext CommandInvocationContext { get; init; }
    }

    public class CommandExecution<TGlobalContext> : CommandExecution where TGlobalContext : class
    {
        /// <summary>
        /// The global context used with command line builder.
        /// </summary>
        public required TGlobalContext GlobalContext { get; init; }
    }
}
