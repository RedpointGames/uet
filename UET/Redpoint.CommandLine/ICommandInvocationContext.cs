namespace Redpoint.CommandLine
{
    using System.CommandLine.Parsing;

    /// <summary>
    /// Provides context for the execution of a command instance. This does not provide access to the original context object used during command line building; if you need to access the context object at runtime, it should be registered as a singleton with <see cref="IRootCommandBuilderApi{TGlobalContext, TSelfType}.AddGlobalRuntimeServices(CommandRuntimeServiceRegistration{TGlobalContext})"/>.
    /// </summary>
    public interface ICommandInvocationContext
    {
        /// <summary>
        /// The result of parsing the command line, including the accessors to get option and argument values.
        /// </summary>
        ParseResult ParseResult { get; }

        /// <summary>
        /// Returns the cancellation token that is cancelled when the user presses Ctrl-C in the console.
        /// </summary>
        /// <returns>The cancellation token that is cancelled when the user presses Ctrl-C in the console.</returns>
        CancellationToken GetCancellationToken();
    }
}
