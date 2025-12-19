namespace Redpoint.CommandLine
{
    using System.CommandLine;

    /// <summary>
    /// Provides additional methods that are specific to building the root command.
    /// </summary>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface IRootCommandBuilderApi<TSelfType>
    {
        /// <summary>
        /// Registers global runtime services that are available for constructor injection when implementations <see cref="ICommandInstance"/> are constructed.
        /// </summary>
        /// <param name="globalRuntimeServices">The callback that should register services in the service collection.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalRuntimeServices(CommandRuntimeServiceRegistration globalRuntimeServices);

        /// <summary>
        /// Registers global parsing services that are available for constructor injection when option classes are constructed in order to parse the command line. Option classes are constructed regardless of whether that particular command is invoked, so you often want to limit what services are registered and available for parsing.
        /// </summary>
        /// <param name="globalParsingServices">The callback that should register services in the minimal service collection used for parsing.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalParsingServices(CommandParsingServiceRegistration globalParsingServices);

        /// <summary>
        /// Sets the global execution handler, which can be used to perform additional wrapping logic around the execution of all commands built with the command line handler. You can use this to catch and log exceptions globally, as well as perform any required startup and shutdown logic around command execution.
        /// </summary>
        /// <param name="commandExecutionHandler">The callback that is used to execute commands.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType SetGlobalExecutionHandler(CommandExecutionHandler commandExecutionHandler);

        /// <summary>
        /// Adds a global option instance to the root command.
        /// </summary>
        /// <param name="globalOption">The global option instance to add to the root command.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalOption(Option globalOption);
    }

    /// <summary>
    /// Provides additional methods that are specific to building the root command.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface IRootCommandBuilderApi<TGlobalContext, TSelfType> where TGlobalContext : class
    {
        /// <summary>
        /// Registers global runtime services that are available for constructor injection when implementations <see cref="ICommandInstance"/> are constructed.
        /// </summary>
        /// <param name="globalRuntimeServices">The callback that should register services in the service collection.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalRuntimeServices(CommandRuntimeServiceRegistration<TGlobalContext> globalRuntimeServices);

        /// <summary>
        /// Registers global parsing services that are available for constructor injection when option classes are constructed in order to parse the command line. Option classes are constructed regardless of whether that particular command is invoked, so you often want to limit what services are registered and available for parsing.
        /// </summary>
        /// <param name="globalParsingServices">The callback that should register services in the minimal service collection used for parsing.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalParsingServices(CommandParsingServiceRegistration<TGlobalContext> globalParsingServices);

        /// <summary>
        /// Sets the global execution handler, which can be used to perform additional wrapping logic around the execution of all commands built with the command line handler. You can use this to catch and log exceptions globally, as well as perform any required startup and shutdown logic around command execution.
        /// </summary>
        /// <param name="commandExecutionHandler">The callback that is used to execute commands.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType SetGlobalExecutionHandler(CommandExecutionHandler<TGlobalContext> commandExecutionHandler);

        /// <summary>
        /// Adds a global option instance to the root command.
        /// </summary>
        /// <param name="globalOption">The global option instance to add to the root command.</param>
        /// <returns>The current command line builder instance.</returns>
        TSelfType AddGlobalOption(Option globalOption);
    }
}
