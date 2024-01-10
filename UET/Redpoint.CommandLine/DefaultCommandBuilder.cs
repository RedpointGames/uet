namespace Redpoint.CommandLine
{
    using System.Diagnostics.CodeAnalysis;

    internal class DefaultCommandBuilder<TGlobalContext> : ICommandBuilder, ICommandBuilder<TGlobalContext> where TGlobalContext : class
    {
        private readonly TGlobalContext _globalContext;
        internal readonly List<BuilderRequestedCommand<TGlobalContext>> _requestedCommands;

        public DefaultCommandBuilder(TGlobalContext globalContext)
        {
            _globalContext = globalContext;
            _requestedCommands = new List<BuilderRequestedCommand<TGlobalContext>>();
        }

        public TGlobalContext GlobalContext => _globalContext;

        public ICommandBuilder<TGlobalContext> AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory<TGlobalContext> commandDescriptorFactory,
            CommandServiceRegistration<TGlobalContext>? additionalRuntimeServices,
            CommandServiceRegistration<TGlobalContext>? additionalParsingServices) where TCommand : class, ICommandInstance where TOptions : class
        {
            _requestedCommands.Add(new BuilderRequestedCommand<TGlobalContext, TCommand, TOptions>
            {
                CommandDescriptorFactory = commandDescriptorFactory,
                AdditionalRuntimeServices = additionalRuntimeServices,
                AdditionalParsingServices = additionalParsingServices,
            });
            return this;
        }

        #region ICommandBuilderApi (without global context) APIs

        ICommandBuilder ICommandBuilderApi<ICommandBuilder>.AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory commandDescriptorFactory,
            CommandServiceRegistration? additionalRuntimeServices,
            CommandServiceRegistration? additionalParsingServices)
        {
            CommandServiceRegistration<TGlobalContext>? specificAdditionalRuntimeServices = null;
            CommandServiceRegistration<TGlobalContext>? specificAdditionalParsingServices = null;
            if (additionalRuntimeServices != null)
            {
                specificAdditionalRuntimeServices = (specificBuilder, services) => additionalRuntimeServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services);
            }
            if (additionalParsingServices != null)
            {
                specificAdditionalParsingServices = (specificBuilder, services) => additionalParsingServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services);
            }

            return (DefaultCommandBuilder<TGlobalContext>)AddCommand<TCommand, TOptions>(
                (specificBuilder) => commandDescriptorFactory((DefaultCommandBuilder<TGlobalContext>)specificBuilder),
                specificAdditionalRuntimeServices,
                specificAdditionalParsingServices);
        }

        #endregion
    }
}
