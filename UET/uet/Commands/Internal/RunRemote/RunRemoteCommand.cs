namespace UET.Commands.Internal.RunRemote
{
    using Fsp;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.GrpcPipes;
    using RemoteHostApi;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using static RemoteHostApi.RemoteHostService;

    internal class RunRemoteCommand
    {
        public sealed class Options
        {
            public Option<string> HostAddress;
            public Option<string> Path;
            public Option<string[]> Arguments;
            public Option<string> WorkingDirectory;
            public Option<string> SyncBackDirectory;

            public Options()
            {
                HostAddress = new Option<string>("--host-address");
                HostAddress.AddAlias("-h");

                Path = new Option<string>("--path") { IsRequired = true };
                Path.AddAlias("-p");

                Arguments = new Option<string[]>("--argument");
                Arguments.AddAlias("-a");

                WorkingDirectory = new Option<string>("--working-directory");
                WorkingDirectory.AddAlias("-w");

                SyncBackDirectory = new Option<string>("--sync-back-directory");
                SyncBackDirectory.AddAlias("-s");
            }
        }

        public static Command CreateRunRemoteCommand()
        {
            var options = new Options();
            var command = new Command("run-remote");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunRemoteCommandInstance>(options);
            return command;
        }

        private sealed class RunRemoteCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunRemoteCommandInstance> _logger;
            private readonly INetworkAutoDiscovery _networkAutoDiscovery;
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly Options _options;

            public RunRemoteCommandInstance(
                ILogger<RunRemoteCommandInstance> logger,
                INetworkAutoDiscovery networkAutoDiscovery,
                IGrpcPipeFactory grpcPipeFactory,
                Options options)
            {
                _logger = logger;
                _networkAutoDiscovery = networkAutoDiscovery;
                _grpcPipeFactory = grpcPipeFactory;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var hostAddress = context.ParseResult.GetValueForOption(_options.HostAddress);
                var path = context.ParseResult.GetValueForOption(_options.Path);
                var arguments = context.ParseResult.GetValueForOption(_options.Arguments) ?? Array.Empty<string>();
                var workingDirectory = context.ParseResult.GetValueForOption(_options.WorkingDirectory);
                var syncBackDirectory = context.ParseResult.GetValueForOption(_options.SyncBackDirectory);

                path = Path.GetFullPath(path!);
                workingDirectory = workingDirectory == null
                    ? Path.GetDirectoryName(path)
                    : Path.GetFullPath(workingDirectory);

                async Task<int?> RunOnHost(IPEndPoint endpoint)
                {
                    _logger.LogInformation($"{endpoint}: Connecting...");
                    var client = _grpcPipeFactory.CreateNetworkClient(
                        endpoint,
                        invoker => new RemoteHostServiceClient(invoker));

                    var request = new RunProcessRequest
                    {
                        RootPath = Path.GetDirectoryName(path),
                        RelativeExecutablePath = Path.GetRelativePath(Path.GetDirectoryName(path)!, path),
                        RelativeWorkingDirectory = Path.GetRelativePath(Path.GetDirectoryName(path)!, workingDirectory!),
                        SyncBackDirectory = syncBackDirectory,
                    };
                    request.Arguments.AddRange(arguments);

                    _logger.LogInformation($"Root path: {request.RootPath}");
                    _logger.LogInformation($"Relative executable path: {request.RelativeExecutablePath}");
                    _logger.LogInformation($"Relative working directory: {request.RelativeWorkingDirectory}");
                    _logger.LogInformation($"Sync back directory: {request.SyncBackDirectory}");
                    _logger.LogInformation($"Argument count: {request.Arguments.Count}");
                    for (int i = 0; i < request.Arguments.Count; i++)
                    {
                        _logger.LogInformation($"Argument #{i + 1}: {request.Arguments[i]}");
                    }

                    try
                    {
                        var response = client.RunProcess(request, cancellationToken: context.GetCancellationToken());
                        while (await response.ResponseStream.MoveNext(context.GetCancellationToken()).ConfigureAwait(false))
                        {
                            switch (response.ResponseStream.Current.ResultCase)
                            {
                                case RunProcessResponse.ResultOneofCase.Started:
                                    _logger.LogInformation($"{endpoint}: Process started.");
                                    break;
                                case RunProcessResponse.ResultOneofCase.StandardOutputLine:
                                    Console.WriteLine(response.ResponseStream.Current.StandardOutputLine);
                                    break;
                                case RunProcessResponse.ResultOneofCase.StandardErrorLine:
                                    Console.WriteLine(response.ResponseStream.Current.StandardErrorLine);
                                    break;
                                case RunProcessResponse.ResultOneofCase.ExitCode:
                                    return response.ResponseStream.Current.ExitCode;
                            }
                        }
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                    {
                        _logger.LogInformation($"{endpoint}: Not available, trying other remote hosts...");
                    }

                    return null;
                }

                if (hostAddress == null)
                {
                    _logger.LogInformation("Discovering remote hosts to run this command on...");

                    await foreach (var service in _networkAutoDiscovery.DiscoverServicesAsync(
                        "_uet-run-remote._tcp.local",
                        context.GetCancellationToken()))
                    {
                        var addresses = service.TargetAddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToList();

                        _logger.LogInformation($"Found remote host: {service.TargetHostname} with {addresses.Count} addresses on port {service.TargetPort}");
                        foreach (var address in addresses)
                        {
                            var result = await RunOnHost(new IPEndPoint(address, service.TargetPort))
                                .ConfigureAwait(false);
                            if (result.HasValue)
                            {
                                return result.Value;
                            }
                        }
                    }

                    _logger.LogError("No remote hosts were available to run this command.");
                    return 1;
                }
                else
                {
                    var result = await RunOnHost(IPEndPoint.Parse(hostAddress))
                        .ConfigureAwait(false);
                    if (result.HasValue)
                    {
                        return result.Value;
                    }

                    _logger.LogError("Remote host did not respond or was unavailable to run this command.");
                    return 1;
                }
            }
        }
    }
}
