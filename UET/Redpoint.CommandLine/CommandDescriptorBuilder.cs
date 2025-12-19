namespace Redpoint.CommandLine
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public class CommandDescriptorBuilder
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        private Type? _optionsType;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        private Type? _instanceType;
        private CommandFactory? _commandFactory;
        private CommandRuntimeServiceRegistration? _runtimeServices;
        private CommandParsingServiceRegistration? _parsingServices;

        internal CommandDescriptorBuilder()
        {
        }

        public static CommandDescriptorBuilder NewBuilder()
        {
            return new CommandDescriptorBuilder();
        }

        public CommandDescriptorBuilder WithOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>()
        {
            _optionsType = typeof(TOptions);
            return this;
        }

        public CommandDescriptorBuilder WithInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommandInstance>() where TCommandInstance : ICommandInstance
        {
            _instanceType = typeof(TCommandInstance);
            return this;
        }

        public CommandDescriptorBuilder WithCommand(CommandFactory commandFactory)
        {
            _commandFactory = commandFactory;
            return this;
        }

        public CommandDescriptorBuilder WithRuntimeServices(CommandRuntimeServiceRegistration serviceRegistration)
        {
            _runtimeServices = serviceRegistration;
            return this;
        }

        public CommandDescriptorBuilder WithParsingServices(CommandParsingServiceRegistration serviceRegistration)
        {
            _parsingServices = serviceRegistration;
            return this;
        }

        public CommandDescriptor Build()
        {
            return new CommandDescriptor
            {
                OptionsType = _optionsType,
                InstanceType = _instanceType,
                CommandFactory = _commandFactory,
                RuntimeServices = _runtimeServices,
                ParsingServices = _parsingServices,
            };
        }
    }

    public class CommandDescriptorBuilder<TGlobalContext> where TGlobalContext : class
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        private Type? _optionsType;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        private Type? _instanceType;
        private CommandFactory<TGlobalContext>? _commandFactory;
        private CommandRuntimeServiceRegistration<TGlobalContext>? _runtimeServices;
        private CommandParsingServiceRegistration<TGlobalContext>? _parsingServices;

        internal CommandDescriptorBuilder()
        {
        }

        public static CommandDescriptorBuilder<TGlobalContext> NewBuilder()
        {
            return new CommandDescriptorBuilder<TGlobalContext>();
        }

        public CommandDescriptorBuilder<TGlobalContext> WithOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>()
        {
            _optionsType = typeof(TOptions);
            return this;
        }

        public CommandDescriptorBuilder<TGlobalContext> WithInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommandInstance>() where TCommandInstance : ICommandInstance
        {
            _instanceType = typeof(TCommandInstance);
            return this;
        }

        public CommandDescriptorBuilder<TGlobalContext> WithCommand(CommandFactory<TGlobalContext> commandFactory)
        {
            _commandFactory = commandFactory;
            return this;
        }

        public CommandDescriptorBuilder<TGlobalContext> WithRuntimeServices(CommandRuntimeServiceRegistration<TGlobalContext> serviceRegistration)
        {
            _runtimeServices = serviceRegistration;
            return this;
        }

        public CommandDescriptorBuilder<TGlobalContext> WithParsingServices(CommandParsingServiceRegistration<TGlobalContext> serviceRegistration)
        {
            _parsingServices = serviceRegistration;
            return this;
        }

        public CommandDescriptor<TGlobalContext> Build()
        {
            return new CommandDescriptor<TGlobalContext>
            {
                OptionsType = _optionsType,
                InstanceType = _instanceType,
                CommandFactory = _commandFactory,
                RuntimeServices = _runtimeServices,
                ParsingServices = _parsingServices,
            };
        }
    }
}
