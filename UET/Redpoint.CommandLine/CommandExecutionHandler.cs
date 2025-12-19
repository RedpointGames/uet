namespace Redpoint.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Parsing;

    /// <summary>
    /// Called to perform additional wrapping logic around the execution of all commands built with the command line handler. You can use this to catch and log exceptions globally, as well as perform any required startup and shutdown logic around command execution.
    /// </summary>
    /// <param name="execution">Information about the command to be executed, including the delegate to invoke to execute the original command.</param>
    public delegate Task<int> CommandExecutionHandler(CommandExecution execution);

    /// <summary>
    /// Called to perform additional wrapping logic around the execution of all commands built with the command line handler. You can use this to catch and log exceptions globally, as well as perform any required startup and shutdown logic around command execution.
    /// </summary>
    /// <param name="execution">Information about the command to be executed, including the delegate to invoke to execute the original command.</param>
    public delegate Task<int> CommandExecutionHandler<TGlobalContext>(CommandExecution<TGlobalContext> execution) where TGlobalContext : class;
}
