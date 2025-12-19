namespace Redpoint.CommandLine
{
    using System.CommandLine;
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
            CommandFactory<TGlobalContext> commandDescriptorFactory,
            CommandRuntimeServiceRegistration<TGlobalContext>? additionalRuntimeServices,
            CommandParsingServiceRegistration<TGlobalContext>? additionalParsingServices) where TCommand : class, ICommandInstance where TOptions : class
        {
            _requestedCommands.Add(new BuilderRequestedCommand<TGlobalContext>
            {
                InstanceType = typeof(TCommand),
                OptionsType = typeof(TOptions),
                CommandFactory = commandDescriptorFactory,
                AdditionalRuntimeServices = additionalRuntimeServices,
                AdditionalParsingServices = additionalParsingServices,
            });
            return this;
        }

        public ICommandBuilder<TGlobalContext> AddCommand<TCommandDescriptorProvider>() where TCommandDescriptorProvider : ICommandDescriptorProvider<TGlobalContext>
        {
            var descriptor = TCommandDescriptorProvider.Descriptor;

            if (descriptor.CommandFactory == null)
            {
                throw new ArgumentException($"The descriptor has no command factory set, from provider '{typeof(TCommandDescriptorProvider).FullName}'.");
            }

            _requestedCommands.Add(new BuilderRequestedCommand<TGlobalContext>
            {
                InstanceType = descriptor.InstanceType,
                OptionsType = descriptor.OptionsType,
                CommandFactory = descriptor.CommandFactory,
                AdditionalRuntimeServices = descriptor.RuntimeServices,
                AdditionalParsingServices = descriptor.ParsingServices,
            });
            return this;
        }

        public ICommandBuilder<TGlobalContext> AddCommandWithoutGlobalContext<TCommandDescriptorProvider>() where TCommandDescriptorProvider : ICommandDescriptorProvider
        {
            ((ICommandBuilderApi<ICommandBuilder>)this).AddCommand<TCommandDescriptorProvider>();
            return this;
        }

        #region ICommandBuilderApi (without global context) APIs

        ICommandBuilder ICommandBuilderApi<ICommandBuilder>.AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandFactory commandDescriptorFactory,
            CommandRuntimeServiceRegistration? additionalRuntimeServices,
            CommandParsingServiceRegistration? additionalParsingServices)
        {
            CommandRuntimeServiceRegistration<TGlobalContext>? specificAdditionalRuntimeServices = null;
            CommandParsingServiceRegistration<TGlobalContext>? specificAdditionalParsingServices = null;
            if (additionalRuntimeServices != null)
            {
                specificAdditionalRuntimeServices = (specificBuilder, services, context) => additionalRuntimeServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services, context);
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

        ICommandBuilder ICommandBuilderApi<ICommandBuilder>.AddCommand<TCommandDescriptorProvider>()
        {
            var descriptor = TCommandDescriptorProvider.Descriptor;

            if (descriptor.CommandFactory == null)
            {
                throw new ArgumentException($"The descriptor has no command factory set, from provider '{typeof(TCommandDescriptorProvider).FullName}'.");
            }

            CommandRuntimeServiceRegistration<TGlobalContext>? specificAdditionalRuntimeServices = null;
            CommandParsingServiceRegistration<TGlobalContext>? specificAdditionalParsingServices = null;
            if (descriptor.RuntimeServices != null)
            {
                specificAdditionalRuntimeServices = (specificBuilder, services, context) => descriptor.RuntimeServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services, context);
            }
            if (descriptor.ParsingServices != null)
            {
                specificAdditionalParsingServices = (specificBuilder, services) => descriptor.ParsingServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services);
            }

            CommandFactory<TGlobalContext> specificCommandFactory = (specificBuilder) => descriptor.CommandFactory((DefaultCommandBuilder<TGlobalContext>)specificBuilder);

            _requestedCommands.Add(new BuilderRequestedCommand<TGlobalContext>
            {
                InstanceType = descriptor.InstanceType,
                OptionsType = descriptor.OptionsType,
                CommandFactory = specificCommandFactory,
                AdditionalRuntimeServices = specificAdditionalRuntimeServices,
                AdditionalParsingServices = specificAdditionalParsingServices,
            });
            return this;
        }

        #endregion
    }
}
