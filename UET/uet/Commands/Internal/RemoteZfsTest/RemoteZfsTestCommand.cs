namespace UET.Commands.Internal.RemoteZfsTest
{
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net;
    using System.Threading.Tasks;
    using Redpoint.Uet.Workspace.RemoteZfs;

    internal sealed class RemoteZfsTestCommand
    {
        public sealed class Options
        {
            public Option<string> Host;
            public Option<int> Port;

            public Options()
            {
                Host = new Option<string>(
                    name: "--host",
                    description: "The host the service is running on.")
                {
                    IsRequired = true,
                };
                Port = new Option<int>(
                    name: "--port",
                    description: "The port for the service is listening on.")
                {
                    IsRequired = true,
                };
            }
        }

        public static Command CreateRemoteZfsTestCommand()
        {
            var options = new Options();
            var command = new Command("remote-zfs-test");
            command.AddAllOptions(options);
            command.AddCommonHandler<RemoteZfsTestCommandInstance>(options);
            return command;
        }

        private sealed class RemoteZfsTestCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<RemoteZfsTestCommandInstance> _logger;
            private readonly IGrpcPipeFactory _grpcPipeFactory;

            public RemoteZfsTestCommandInstance(
                Options options,
                ILogger<RemoteZfsTestCommandInstance> logger,
                IGrpcPipeFactory grpcPipeFactory)
            {
                _options = options;
                _logger = logger;
                _grpcPipeFactory = grpcPipeFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var host = context.ParseResult.GetValueForOption(_options.Host);
                var port = context.ParseResult.GetValueForOption(_options.Port);

                if (string.IsNullOrWhiteSpace(host))
                {
                    _logger.LogError("Invalid or missing arguments.");
                    return 1;
                }

                _logger.LogInformation($"Connecting...");
                var client = _grpcPipeFactory.CreateNetworkClient(
                    IPEndPoint.Parse(host),
                    invoker => new RemoteZfs.RemoteZfsClient(invoker));

                _logger.LogInformation($"Requesting share...");
                var response = client.Acquire(cancellationToken: context.GetCancellationToken());

                _logger.LogInformation($"Waiting for acquisition...");
                var acquired = await response.ResponseStream.MoveNext(context.GetCancellationToken()).ConfigureAwait(false);
                if (!acquired)
                {
                    _logger.LogError($"Server failed to acquire.");
                    return 1;
                }

                _logger.LogInformation($"Allocated share: {response.ResponseStream.Current.WindowsSharePath}");

                _logger.LogInformation($"Waiting 10 seconds...");
                await Task.Delay(10000, context.GetCancellationToken()).ConfigureAwait(false);

                _logger.LogInformation($"Closing request stream...");
                await response.RequestStream.CompleteAsync().ConfigureAwait(false);

                return 0;
            }
        }
    }
}
