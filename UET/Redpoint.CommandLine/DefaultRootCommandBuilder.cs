namespace Redpoint.CommandLine
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// The implementation of the root command line builder.
    /// </summary>
    internal class DefaultCommandLineBuilder<TGlobalContext> : ICommandLineBuilder, ICommandLineBuilder<TGlobalContext> where TGlobalContext : class
    {
        private readonly TGlobalContext _globalContext;
        private readonly List<BuilderRequestedCommand<TGlobalContext>> _requestedCommands;
        private readonly List<Option> _globalOptions;
        private readonly List<CommandServiceRegistration<TGlobalContext>> _globalParsingServices;
        private readonly List<CommandPostParseServiceRegistration<TGlobalContext>> _globalRuntimeServices;
        private CommandExecutionHandler? _commandExecutionHandler;

        public DefaultCommandLineBuilder(TGlobalContext globalContext)
        {
            _globalContext = globalContext;
            _requestedCommands = new List<BuilderRequestedCommand<TGlobalContext>>();
            _globalOptions = new List<Option>();
            _globalParsingServices = new List<CommandServiceRegistration<TGlobalContext>>();
            _globalRuntimeServices = new List<CommandPostParseServiceRegistration<TGlobalContext>>();
            _commandExecutionHandler = null;
        }

        public TGlobalContext GlobalContext => _globalContext;

        public ICommandLineBuilder<TGlobalContext> AddCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(
            CommandDescriptorFactory<TGlobalContext> commandDescriptorFactory,
            CommandPostParseServiceRegistration<TGlobalContext>? additionalRuntimeServices = null,
            CommandServiceRegistration<TGlobalContext>? additionalParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class
        {
            _requestedCommands.Add(new BuilderRequestedCommand<TGlobalContext, TCommand, TOptions>
            {
                CommandDescriptorFactory = commandDescriptorFactory,
                AdditionalRuntimeServices = additionalRuntimeServices,
                AdditionalParsingServices = additionalParsingServices,
            });
            return this;
        }

        public ICommandLineBuilder<TGlobalContext> AddGlobalOption(Option globalOption)
        {
            _globalOptions.Add(globalOption);
            return this;
        }

        public ICommandLineBuilder<TGlobalContext> AddGlobalParsingServices(CommandServiceRegistration<TGlobalContext> globalParsingServices)
        {
            _globalParsingServices.Add(globalParsingServices);
            return this;
        }

        public ICommandLineBuilder<TGlobalContext> AddGlobalRuntimeServices(CommandPostParseServiceRegistration<TGlobalContext> globalRuntimeServices)
        {
            _globalRuntimeServices.Add(globalRuntimeServices);
            return this;
        }

        public ICommandLineBuilder<TGlobalContext> SetGlobalExecutionHandler(CommandExecutionHandler commandExecutionHandler)
        {
            ArgumentNullException.ThrowIfNull(commandExecutionHandler);
            if (_commandExecutionHandler != null)
            {
                throw new ArgumentException("SetGlobalExecutionHandler has already been called on this command line builder.");
            }
            _commandExecutionHandler = commandExecutionHandler;
            return this;
        }

        private void RegisterCommandsToParentCommandDescriptor(
            Command parentCommandDescriptor,
            List<BuilderRequestedCommand<TGlobalContext>> requestedCommands)
        {
            // Iterate through and add all of the commands to the root command.
            foreach (var requestedCommand in requestedCommands)
            {
                // Build the command descriptor.
                var commandBuilder = new DefaultCommandBuilder<TGlobalContext>(_globalContext);
                var commandDescriptor = requestedCommand.CommandDescriptorFactory(commandBuilder);

                // Create the service provider for option parsing. This service provider MUST NOT
                // be disposed, as references can be held to it by the command arguments and options
                // set up in the Options object, which exists beyond the lifetime of Build().
                var parsingServiceCollection = new ServiceCollection();
                parsingServiceCollection.AddTransient(requestedCommand.OptionsType);
                foreach (var parsingServices in _globalParsingServices)
                {
                    parsingServices(this, parsingServiceCollection);
                }
                requestedCommand.AdditionalParsingServices?.Invoke(this, parsingServiceCollection);
                var parsingServiceProvider = parsingServiceCollection.BuildServiceProvider();

                // Get the options instance via dependency injection.
                var options = parsingServiceProvider.GetRequiredService(requestedCommand.OptionsType);

                // Use reflection to add all arguments and options from the options instance.
                foreach (var argument in requestedCommand.OptionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.PropertyType.IsAssignableTo(typeof(Argument)))
                    .Select(x => (Argument)x.GetValue(options)!))
                {
                    commandDescriptor.AddArgument(argument);
                }
                foreach (var argument in requestedCommand.OptionsType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.FieldType.IsAssignableTo(typeof(Argument)))
                    .Select(x => (Argument)x.GetValue(options)!))
                {
                    commandDescriptor.AddArgument(argument);
                }
                foreach (var option in requestedCommand.OptionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.PropertyType.IsAssignableTo(typeof(Option)))
                    .Select(x => (Option)x.GetValue(options)!))
                {
                    commandDescriptor.AddOption(option);
                }
                foreach (var option in requestedCommand.OptionsType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.FieldType.IsAssignableTo(typeof(Option)))
                    .Select(x => (Option)x.GetValue(options)!))
                {
                    commandDescriptor.AddOption(option);
                }

                // Set the command handler.
                commandDescriptor.SetHandler(async (systemCommandLineInvocationContext) =>
                {
                    var invocationContext = new SafeCommandInvocationContext(systemCommandLineInvocationContext);

                    var runtimeServiceCollection = new ServiceCollection();
                    runtimeServiceCollection.AddSingleton<ICommandInvocationContext>(invocationContext);
                    runtimeServiceCollection.AddSingleton(requestedCommand.OptionsType, options);
                    runtimeServiceCollection.AddSingleton(requestedCommand.CommandType);
                    foreach (var runtimeServices in _globalRuntimeServices)
                    {
                        runtimeServices(this, runtimeServiceCollection, invocationContext);
                    }
                    requestedCommand.AdditionalRuntimeServices?.Invoke(this, runtimeServiceCollection, invocationContext);

                    await using (runtimeServiceCollection.BuildServiceProvider().AsAsyncDisposable(out var runtimeServiceProvider).ConfigureAwait(false))
                    {
                        var commandInstance = (ICommandInstance)runtimeServiceProvider.GetRequiredService(requestedCommand.CommandType);

                        if (_commandExecutionHandler != null)
                        {
                            systemCommandLineInvocationContext.ExitCode = await _commandExecutionHandler(runtimeServiceProvider, async () =>
                            {
                                return await commandInstance.ExecuteAsync(invocationContext).ConfigureAwait(false);
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            systemCommandLineInvocationContext.ExitCode = await commandInstance.ExecuteAsync(invocationContext).ConfigureAwait(false);
                        }
                    }
                });

                // If the command builder has any subcommands requested, bind those now.
                RegisterCommandsToParentCommandDescriptor(commandDescriptor, commandBuilder._requestedCommands);

                // Add the built command to the parent command descriptor.
                parentCommandDescriptor.AddCommand(commandDescriptor);
            }
        }

        public Command Build(string description)
        {
            var rootCommand = new RootCommand(description);

            // Add all the global options.
            foreach (var globalOption in _globalOptions)
            {
                rootCommand.AddOption(globalOption);
            }

            // Register all of the subcommands with the root command.
            RegisterCommandsToParentCommandDescriptor(rootCommand, _requestedCommands);

            return rootCommand;
        }

        #region ICommandLineBuilder (without global context) APIs

        ICommandLineBuilder ICommandBuilderApi<ICommandLineBuilder>.AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(CommandDescriptorFactory commandDescriptorFactory, CommandPostParseServiceRegistration? additionalRuntimeServices, CommandServiceRegistration? additionalParsingServices)
        {
            CommandPostParseServiceRegistration<TGlobalContext>? specificAdditionalRuntimeServices = null;
            CommandServiceRegistration<TGlobalContext>? specificAdditionalParsingServices = null;
            if (additionalRuntimeServices != null)
            {
                specificAdditionalRuntimeServices = (specificBuilder, services, context) => additionalRuntimeServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services, context);
            }
            if (additionalParsingServices != null)
            {
                specificAdditionalParsingServices = (specificBuilder, services) => additionalParsingServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services);
            }

            return (DefaultCommandLineBuilder<TGlobalContext>)AddCommand<TCommand, TOptions>(
                (specificBuilder) => commandDescriptorFactory((DefaultCommandBuilder<TGlobalContext>)specificBuilder),
                specificAdditionalRuntimeServices,
                specificAdditionalParsingServices);
        }

        ICommandLineBuilder IRootCommandBuilderApi<ICommandLineBuilder>.AddGlobalOption(Option globalOption)
        {
            return (DefaultCommandLineBuilder<TGlobalContext>)AddGlobalOption(globalOption);
        }

        ICommandLineBuilder IRootCommandBuilderApi<ICommandLineBuilder>.AddGlobalParsingServices(CommandServiceRegistration globalParsingServices)
        {
            return (DefaultCommandLineBuilder<TGlobalContext>)AddGlobalParsingServices(
                (specificBuilder, services) => globalParsingServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services));
        }

        ICommandLineBuilder IRootCommandBuilderApi<ICommandLineBuilder>.AddGlobalRuntimeServices(CommandPostParseServiceRegistration globalRuntimeServices)
        {
            return (DefaultCommandLineBuilder<TGlobalContext>)AddGlobalRuntimeServices(
                (specificBuilder, services, context) => globalRuntimeServices((DefaultCommandLineBuilder<TGlobalContext>)specificBuilder, services, context));
        }

        ICommandLineBuilder IRootCommandBuilderApi<ICommandLineBuilder>.SetGlobalExecutionHandler(CommandExecutionHandler commandExecutionHandler)
        {
            return (DefaultCommandLineBuilder<TGlobalContext>)SetGlobalExecutionHandler(commandExecutionHandler);
        }

        Command ICommandLineBuilder.Build(string description)
        {
            return Build(description);
        }

        #endregion
    }
}
