namespace Redpoint.CommandLine
{
    using System.CommandLine;

    /// <summary>
    /// The basic interface to be implemented by all commands; implementations of this interface are instantiated through dependency injection, so you can inject services into their constructors.
    /// </summary>
    public interface ICommandInstance
    {
        /// <summary>
        /// Called by command line library when the root command returned from <see cref="ICommandLineBuilder{TGlobalContext}.Build(string)"/> has <see cref="CommandExtensions.InvokeAsync(Command, string, IConsole?)"/> called on it.
        /// </summary>
        /// <param name="context">The invocation context for the command.</param>
        /// <returns>The exit code for the application.</returns>
        Task<int> ExecuteAsync(ICommandInvocationContext context);
    }
}
