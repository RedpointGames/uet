namespace Redpoint.CommandLine
{
    /// <summary>
    /// Called to perform additional wrapping logic around the execution of all commands built with the command line handler. You can use this to catch and log exceptions globally, as well as perform any required startup and shutdown logic around command execution.
    /// </summary>
    /// <param name="runtimeServiceProvider">The runtime service provider used for executing the command.</param>
    /// <param name="executeCommand">The delegate to invoke when the command should be executed.</param>
    public delegate Task<int> CommandExecutionHandler(IServiceProvider runtimeServiceProvider, Func<Task<int>> executeCommand);
}
