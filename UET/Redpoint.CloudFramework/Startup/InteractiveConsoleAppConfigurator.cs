namespace Redpoint.CloudFramework.Startup
{
    // @note: This needs to be completely redone based on the command-line infrastructure that we wrote
    // for UET, but we don't have any active projects that are using InteractiveConsoleAppConfigurator
    // so we don't need to refactor this now.
#if ENABLE_UNSUPPORTED
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Tracing;
    using System;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.IO;
    using System.CommandLine.Parsing;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using static System.Environment;

    public interface IInteractiveConsoleAppConfigurator : IBaseConfigurator<IInteractiveConsoleAppConfigurator>
    {
        IInteractiveConsoleAppConfigurator UseCommand(RootCommand rootCommand);

        IInteractiveConsoleAppConfigurator UseServiceConfiguration(Action<IServiceCollection> configureServices);

        IInteractiveConsoleAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig);

        [RequiresUnreferencedCode("This implementation scans the AppDomain for all implementations of Command.")]
        Task<int> StartInteractiveConsoleApp(string[] args);
    }

    internal class InteractiveConsoleAppConfigurator : BaseConfigurator<IInteractiveConsoleAppConfigurator>, IInteractiveConsoleAppConfigurator
    {
        private RootCommand? _rootCommand;
        private Action<IServiceCollection>? _configureServices;
        private Func<IConfiguration, string, HelmConfiguration>? _helmConfig;

        public InteractiveConsoleAppConfigurator()
        {
            _isInteractiveCLIApp = true;
        }

        public IInteractiveConsoleAppConfigurator UseCommand(RootCommand rootCommand)
        {
            _rootCommand = rootCommand;
            return this;
        }

        public IInteractiveConsoleAppConfigurator UseServiceConfiguration(Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices;
            return this;
        }

        public IInteractiveConsoleAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig)
        {
            _helmConfig = helmConfig;
            return this;
        }

        [RequiresUnreferencedCode("This implementation scans the AppDomain for all implementations of Command.")]
        public async Task<int> StartInteractiveConsoleApp(string[] args)
        {
            ValidateConfiguration();
            if (_rootCommand == null)
            {
                throw new InvalidOperationException("You must specify the root command by calling UseCommand(command).");
            }

            var productionOption = new Option<bool>("--production", "If this flag is passed, this CLI connects to the production database directly. You must also pass --production-project-id and --production-redis-server.");
            var productionProjectIdOption = new Option<string>("--production-project-id", "The Google Cloud project ID to use with --production.") { ArgumentHelpName = "project-id" };
            var productionRedisServerOption = new Option<string>("--production-redis-server", "The Redis server to connect to for use with --production. You'll typically need to forward a connection to the Redis server using kubectl port-forward.") { ArgumentHelpName = "127.0.0.1:6379" };
            _rootCommand.AddGlobalOption(productionOption);
            _rootCommand.AddGlobalOption(productionProjectIdOption);
            _rootCommand.AddGlobalOption(productionRedisServerOption);

            var rootArgs = _rootCommand.Parse(args);
            var isProduction = rootArgs.GetValueForOption<bool>(productionOption);
            if (isProduction)
            {
                if (string.IsNullOrWhiteSpace(rootArgs.GetValueForOption<string>(productionProjectIdOption)) ||
                    string.IsNullOrWhiteSpace(rootArgs.GetValueForOption<string>(productionRedisServerOption)))
                {
                    Console.Error.WriteLine("--production-project-id and --production-redis-server must both be set!");
                    return 1;
                }

                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "gcloud", "application_default_credentials.json"));
                Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT_ID", rootArgs.GetValueForOption<string>(productionProjectIdOption));
                Environment.SetEnvironmentVariable("REDIS_SERVER", rootArgs.GetValueForOption<string>(productionRedisServerOption));
            }

            var parser = new CommandLineBuilder(_rootCommand)
                .UseHost((IHostBuilder hostBuilder) =>
                {
                    hostBuilder = hostBuilder
                        .UseEnvironment(isProduction ? "Production" : "Development")
                        .ConfigureServices((context, services) =>
                        {
                            if (!isProduction)
                            {
                                if (_helmConfig == null)
                                {
                                    services.AddSingleton<IHostedService, DevelopmentStartup>(sp =>
                                    {
                                        return new DevelopmentStartup(
                                            sp.GetRequiredService<IHostEnvironment>(),
                                            sp.GetRequiredService<ILogger<DevelopmentStartup>>(),
                                            _googleCloudUsage,
                                            sp.GetRequiredService<IConfiguration>(),
                                            (_, _) => Array.Empty<DevelopmentDockerContainer>());
                                    });
                                }
                                else if (context.HostingEnvironment.IsDevelopment())
                                {
                                    var helmConfig = _helmConfig(context.Configuration, context.HostingEnvironment.ContentRootPath);
                                    services.AddSingleton<IOptionalHelmConfiguration>(new BoundHelmConfiguration(helmConfig));
                                    Environment.SetEnvironmentVariable("REDIS_SERVER", "localhost:" + helmConfig.RedisPort);
                                }
                            }
                        })
                        .ConfigureServices((context, services) =>
                        {
                            services.AddSingleton<IManagedTracer, NullManagedTracer>();
                            services.AddSingleton<IConsole, SystemConsole>();

                            this.PreStartupConfigureServices(context.HostingEnvironment, services);
                            _configureServices?.Invoke(services);
                            this.PostStartupConfigureServices(services);
                        })
                        .ConfigureAppConfiguration((hostingContext, config) =>
                        {
                            ConfigureAppConfiguration(hostingContext.HostingEnvironment, config);
                        });
                    // Bind all the commands.
                    foreach (var commandType in AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => typeof(Command).IsAssignableFrom(x)))
                    {
                        var handlerType = commandType.GetNestedType("Handler");
                        if (handlerType != null)
                        {
                            hostBuilder.UseCommandHandler(commandType, handlerType);
                        }
                    }
                })
                .UseHelp()
                .Build();
            return await parser.InvokeAsync(args).ConfigureAwait(false);
        }
    }
#endif
}
