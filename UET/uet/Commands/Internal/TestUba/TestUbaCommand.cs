namespace UET.Commands.Internal.TestUba
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Concurrency;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uba;
    using Redpoint.Uba.Native;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Services;

    internal sealed class TestUbaCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<TestUbaCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("test-uba");
                })
            .Build();

        internal sealed class Options
        {
            public Option<DirectoryInfo> UbaPath;
            public Option<string> RemoteAgentIp;
            public Option<int> RemoteAgentPort;
            public Option<FileInfo> CommandPath;

            public Options()
            {
                UbaPath = new Option<DirectoryInfo>("--uba-path") { IsRequired = true };
                RemoteAgentIp = new Option<string>("--remote-agent-ip") { IsRequired = true };
                RemoteAgentPort = new Option<int>("--remote-agent-port") { IsRequired = true };
                CommandPath = new Option<FileInfo>("--command-path") { IsRequired = true };
            }
        }

        private sealed class TestUbaCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestUbaCommandInstance> _logger;
            private readonly IUbaServerFactory _ubaServerFactory;
            private readonly IPathResolver _pathResolver;
            private readonly Options _options;

            public TestUbaCommandInstance(
                ILogger<TestUbaCommandInstance> logger,
                IUbaServerFactory ubaServerFactory,
                IPathResolver pathResolver,
                Options options)
            {
                _logger = logger;
                _ubaServerFactory = ubaServerFactory;
                _pathResolver = pathResolver;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                UbaNative.Init(context.ParseResult.GetValueForOption(_options.UbaPath)!.FullName);

                // Set up the server that will dispatch processes.
                await using (_ubaServerFactory
                    .CreateServer(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)!, "Epic", "UnrealBuildAccelerator"),
                        Path.Combine(Environment.CurrentDirectory, "UbaTrace.log"))
                    .AsAsyncDisposable(out var server)
                    .ConfigureAwait(false))
                {
                    // Connect to the remote agent that will run processes.
                    var ip = context.ParseResult.GetValueForOption(_options.RemoteAgentIp)!;
                    var port = context.ParseResult.GetValueForOption(_options.RemoteAgentPort);
                    if (!server.AddRemoteAgent(ip, port))
                    {
                        _logger.LogError($"Failed to add remote agent: {ip}:{port}");
                        return 1;
                    }

                    // Run the command through UBA.
                    var commandPath = context.ParseResult.GetValueForOption(_options.CommandPath)!.FullName;
                    _logger.LogInformation($"Running command: {commandPath} --help");
                    try
                    {
                        var exitCode = await server.ExecuteAsync(
                            new UbaProcessSpecification
                            {
                                FilePath = commandPath,
                                Arguments = ["--help"],
                                PreferRemote = true,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken()).ConfigureAwait(false);
                        _logger.LogInformation($"Received exit code: {exitCode}");
                        return exitCode;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation($"Execution cancelled.");
                        return 1;
                    }
                }
            }
        }
    }
}
