namespace Redpoint.CommandLine
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents the API surface of a command builder.
    /// </summary>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface ICommandBuilderApi<out TSelfType>
    {
        /// <summary>
        /// Adds a subcommand to this command.
        /// </summary>
        /// <typeparam name="TCommand">The <see cref="ICommandInstance"/> implementation that will be constructed when the command is executed.</typeparam>
        /// <typeparam name="TOptions">The options class which will be constructed to parse arguments and, when the command is executed, available through dependency injection as an injectable service into the <see cref="ICommandInstance"/> implementation's constructor.</typeparam>
        /// <param name="commandDescriptorFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <param name="additionalRuntimeServices">An additional set of services that should be available when the <see cref="ICommandInstance"/> is being constructed at runtime.</param>
        /// <param name="additionalParsingServices">An additional set of services that should be available when constructing the options class.</param>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory commandDescriptorFactory,
            CommandPostParseServiceRegistration? additionalRuntimeServices = null,
            CommandServiceRegistration? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;

        /// <summary>
        /// Adds a subcommand to this command that doesn't handle execution itself, but instead only has subcommands underneath it.
        /// </summary>
        /// <param name="commandDescriptorFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <returns>The current command builder.</returns>
        TSelfType AddUnhandledCommand(CommandDescriptorFactory commandDescriptorFactory);
    }

    /// <summary>
    /// Represents the API surface of a command builder.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    /// <typeparam name="TSelfType">The interface that is implementing this interface.</typeparam>
    public interface ICommandBuilderApi<TGlobalContext, out TSelfType> : IHasGlobalContext<TGlobalContext> where TGlobalContext : class
    {
        /// <summary>
        /// Adds a subcommand to this command.
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <typeparam name="TOptions"></typeparam>
        /// <param name="commandDescriptorFactory"></param>
        /// <param name="additionalRuntimeServices"></param>
        /// <param name="additionalParsingServices"></param>
        /// <returns></returns>
        TSelfType AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory<TGlobalContext> commandDescriptorFactory,
            CommandPostParseServiceRegistration<TGlobalContext>? additionalRuntimeServices = null,
            CommandServiceRegistration<TGlobalContext>? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;

        /// <summary>
        /// Adds a subcommand to this command that doesn't handle execution itself, but instead only has subcommands underneath it.
        /// </summary>
        /// <param name="commandDescriptorFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <returns>The current command builder.</returns>
        TSelfType AddUnhandledCommand(CommandDescriptorFactory<TGlobalContext> commandDescriptorFactory);
    }
}
