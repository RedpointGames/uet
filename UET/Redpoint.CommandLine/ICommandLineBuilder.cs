namespace Redpoint.CommandLine
{
    using System.CommandLine;

    /// <summary>
    /// A command line builder which can be used to construct a command line for a console application. After configuring the command line, call <see cref="Build"/> to construct the <see cref="Command"/> instance that can be used for parsing and execution.
    /// </summary>
    public interface ICommandLineBuilder : IRootCommandBuilderApi<ICommandLineBuilder>, ICommandBuilderApi<ICommandLineBuilder>
    {
        /// <summary>
        /// Build the final <see cref="Command"/> object that can be used for parsing and execution. You should call <see cref="CommandExtensions.InvokeAsync(Command, string[], IConsole?)"/> on this instance.
        /// </summary>
        /// <param name="description">The description of the root command.</param>
        /// <returns>The final <see cref="Command"/> instance.</returns>
        Command Build(string description = "");
    }

    /// <summary>
    /// A command line builder which can be used to construct a command line for a console application. After configuring the command line, call <see cref="Build"/> to construct the <see cref="Command"/> instance that can be used for parsing and execution.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    public interface ICommandLineBuilder<TGlobalContext> : IRootCommandBuilderApi<TGlobalContext, ICommandLineBuilder<TGlobalContext>>, ICommandBuilderApi<TGlobalContext, ICommandLineBuilder<TGlobalContext>> where TGlobalContext : class
    {
        /// <summary>
        /// Build the final <see cref="Command"/> object that can be used for parsing and execution. You should call <see cref="CommandExtensions.InvokeAsync(Command, string[], IConsole?)"/> on this instance.
        /// </summary>
        /// <param name="description">The description of the root command.</param>
        /// <returns>The final <see cref="Command"/> instance.</returns>
        Command Build(string description = "");
    }
}
