namespace UET.Commands.Internal.RemoteZfsServer
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using RemoteZfsServer = Redpoint.Uet.Workspace.RemoteZfs.RemoteZfsServer;

    internal sealed class RemoteZfsServerCommand
    {
        public sealed class Options
        {
            public Option<int> Port;
            public Option<string> ZvolRoot;
            public Option<string> ZvolSource;
            public Option<string> WindowsShareNetworkPrefix;

            public Options()
            {
                Port = new Option<int>(
                    name: "--port",
                    description: "The port for the service to listen on.")
                {
                    IsRequired = true,
                };
                ZvolRoot = new Option<string>(
                    name: "--zvol-root",
                    description: "The zvol which contains the source volume and which should contain our temporary created volumes.")
                {
                    IsRequired = true,
                };
                ZvolSource = new Option<string>(
                    name: "--zvol-source",
                    description: "The name of the source volume to snapshot.")
                {
                    IsRequired = true,
                };
                WindowsShareNetworkPrefix = new Option<string>(
                    name: "--windows-share-network-prefix",
                    description: "The Windows share prefix to prepend to the new volume name for clients to access the volume at.")
                {
                    IsRequired = true,
                };
            }
        }

        public static Command CreateRemoteZfsServerCommand()
        {
            var options = new Options();
            var command = new Command("remote-zfs-server");
            command.AddAllOptions(options);
            command.AddCommonHandler<RemoteZfsServerCommandInstance>(options);
            return command;
        }

        private sealed class RemoteZfsServerCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<RemoteZfsServerCommandInstance> _logger;
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly IServiceProvider _serviceProvider;

            public RemoteZfsServerCommandInstance(
                Options options,
                ILogger<RemoteZfsServerCommandInstance> logger,
                IGrpcPipeFactory grpcPipeFactory,
                IServiceProvider serviceProvider)
            {
                _options = options;
                _logger = logger;
                _grpcPipeFactory = grpcPipeFactory;
                _serviceProvider = serviceProvider;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsLinux())
                {
                    _logger.LogError("This command can only be used on Linux (with ZFS).");
                    return 1;
                }

                var port = context.ParseResult.GetValueForOption(_options.Port);
                var zvolRoot = context.ParseResult.GetValueForOption(_options.ZvolRoot);
                var zvolSource = context.ParseResult.GetValueForOption(_options.ZvolSource);
                var windowsShareNetworkPrefix = context.ParseResult.GetValueForOption(_options.WindowsShareNetworkPrefix);

                if (string.IsNullOrWhiteSpace(zvolRoot) ||
                    string.IsNullOrWhiteSpace(zvolSource) ||
                    string.IsNullOrWhiteSpace(windowsShareNetworkPrefix))
                {
                    _logger.LogError("Invalid or missing arguments.");
                    return 1;
                }

                var remoteZfsServer = new RemoteZfsServer(
                    _serviceProvider.GetRequiredService<IProcessExecutor>(),
                    _serviceProvider.GetRequiredService<ILogger<RemoteZfsServer>>(),
                    zvolRoot,
                    zvolSource,
                    windowsShareNetworkPrefix);

                try
                {
                    await using (_grpcPipeFactory.CreateNetworkServer(remoteZfsServer, networkPort: port).AsAsyncDisposable(out var server).ConfigureAwait(false))
                    {
                        await server.StartAsync().ConfigureAwait(false);

                        _logger.LogInformation($"Remote ZFS snapshot service has started on port {server.NetworkPort}.");

                        while (!context.GetCancellationToken().IsCancellationRequested)
                        {
                            await Task.Delay(1000, context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Remote ZFS snapshot service has stopped.");
                }

                return 0;
            }
        }
    }
}
