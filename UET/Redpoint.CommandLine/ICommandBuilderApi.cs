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
        /// <param name="commandDescriptorFactory">The callback that constructs the <see cref="System.CommandLine.Command"/> that describes the command.</param>
        /// <param name="additionalRuntimeServices">An additional set of services that should be available when the <see cref="ICommandInstance"/> is being constructed at runtime.</param>
        /// <param name="additionalParsingServices">An additional set of services that should be available when constructing the options class.</param>
        /// <returns>The current command builder.</returns>
        TSelfType AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory commandDescriptorFactory,
            CommandServiceRegistration? additionalRuntimeServices = null,
            CommandServiceRegistration? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;
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
            CommandServiceRegistration<TGlobalContext>? additionalRuntimeServices = null,
            CommandServiceRegistration<TGlobalContext>? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class;
    }
}
