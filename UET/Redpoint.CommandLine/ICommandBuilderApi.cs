namespace Redpoint.CommandLine
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents the API surface of a command builder.
    /// </summary>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface ICommandBuilderApi<TSelfType>
    {
        /// <summary>
        /// Adds a sub-command to this command.
        /// </summary>
        /// <typeparam name="TCommand">The <see cref="ICommandInstance"/> implementation that will be constructed when the command is executed.</typeparam>
        /// <typeparam name="TOptions">The options class which will be constructed to parse arguments and, when the command is executed, available through dependency injection as an injectable service into the <see cref="ICommandInstance"/> implementation's constructor.</typeparam>
        /// <param name="commandFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <param name="additionalRuntimeServices">An additional set of services that should be available when the <see cref="ICommandInstance"/> is being constructed at runtime.</param>
        /// <param name="additionalParsingServices">An additional set of services that should be available when constructing the options class.</param>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandFactory commandFactory,
            CommandRuntimeServiceRegistration? additionalRuntimeServices = null,
            CommandParsingServiceRegistration? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;

        /// <summary>
        /// Adds a sub-command to this command.
        /// </summary>
        /// <typeparam name="TCommandDescriptorProvider">The <see cref="ICommandDescriptorProvider"/> implementation that provides the descriptor for the command.</typeparam>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommand<TCommandDescriptorProvider>() where TCommandDescriptorProvider : ICommandDescriptorProvider;
    }

    /// <summary>
    /// Represents the API surface of a command builder.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface ICommandBuilderApi<TGlobalContext, TSelfType> : IHasGlobalContext<TGlobalContext> where TGlobalContext : class
    {
        /// <summary>
        /// Adds a sub-command to this command.
        /// </summary>
        /// <typeparam name="TCommand">The <see cref="ICommandInstance"/> implementation that will be constructed when the command is executed.</typeparam>
        /// <typeparam name="TOptions">The options class which will be constructed to parse arguments and, when the command is executed, available through dependency injection as an injectable service into the <see cref="ICommandInstance"/> implementation's constructor.</typeparam>
        /// <param name="commandFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <param name="additionalRuntimeServices">An additional set of services that should be available when the <see cref="ICommandInstance"/> is being constructed at runtime.</param>
        /// <param name="additionalParsingServices">An additional set of services that should be available when constructing the options class.</param>
        /// <returns></returns>
        TSelfType AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandFactory<TGlobalContext> commandFactory,
            CommandRuntimeServiceRegistration<TGlobalContext>? additionalRuntimeServices = null,
            CommandParsingServiceRegistration<TGlobalContext>? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;

        /// <summary>
        /// Adds a sub-command to this command.
        /// </summary>
        /// <typeparam name="TCommandDescriptorProvider">The <see cref="ICommandDescriptorProvider"/> implementation that provides the descriptor for the command.</typeparam>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommand<TCommandDescriptorProvider>() where TCommandDescriptorProvider : ICommandDescriptorProvider<TGlobalContext>;

        /// <summary>
        /// Adds a sub-command to this command, without the global context. This is useful if you are pulling in commands from a library that is not aware of the global context.
        /// </summary>
        /// <typeparam name="TCommandDescriptorProvider">The <see cref="ICommandDescriptorProvider"/> implementation that provides the descriptor for the command.</typeparam>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommandWithoutGlobalContext<TCommandDescriptorProvider>() where TCommandDescriptorProvider : ICommandDescriptorProvider;
    }
}
