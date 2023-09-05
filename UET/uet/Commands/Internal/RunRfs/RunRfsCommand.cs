namespace UET.Commands.Internal.RunRfs
{
    using Fsp;
    using Grpc.Net.Client;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Redpoint.Logging;
    using Redpoint.Rfs.WinFsp;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;

    internal class RunRfsCommand
    {
        public class Options
        {
            public Option<string?> Path = new Option<string?>("--path", "The path to serve the remote filesystem from.");
            public Option<string?> ConnectTo = new Option<string?>("--connect-to", "The address of the RFS host to connect to.");
            public Option<int?> ServingPort = new Option<int?>("--serving-port", "The port to serve RFS on this machine.");
            public Option<string[]> Junctions = new Option<string[]>("--junction", "An additional path to junction to the local machine.");
        }

        public static Command CreateRunRfsCommand()
        {
            var options = new Options();
            var command = new Command("run-rfs");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunRfsCommandInstance>(options);
            return command;
        }

        private class RunRfsCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunRfsCommandInstance> _logger;
            private readonly Options _options;

            public RunRfsCommandInstance(
                ILogger<RunRfsCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                {
                    return 1;
                }

                var path = context.ParseResult.GetValueForOption(_options.Path);
                var connectTo = context.ParseResult.GetValueForOption(_options.ConnectTo);
                var servingPort = context.ParseResult.GetValueForOption(_options.ServingPort);
                var junctions = context.ParseResult.GetValueForOption(_options.Junctions) ?? Array.Empty<string>();

                WebApplication? app = null;
                if (servingPort != null || (connectTo == null && path != null))
                {
                    var builder = WebApplication.CreateBuilder();
                    builder.Logging.ClearProviders();
                    builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
                    builder.Services.AddGrpc(options =>
                    {
                        // Allow unlimited message sizes.
                        options.MaxReceiveMessageSize = null;
                        options.MaxSendMessageSize = null;
                    });
                    builder.Services.Add(new ServiceDescriptor(
                        typeof(WindowsRfs.WindowsRfsBase),
                        new WindowsRfsHost(_logger)));
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Listen(
                            servingPort == null
                                ? new IPEndPoint(IPAddress.Loopback, 0)
                                : new IPEndPoint(IPAddress.Any, servingPort.Value),
                            listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                    });

                    app = builder.Build();
                    app.UseRouting();
                    app.UseGrpcWeb();
                    app.MapGrpcService<WindowsRfs.WindowsRfsBase>();

                    await app.StartAsync();

                    servingPort = new Uri(app.Urls.First()).Port;

                    _logger.LogInformation($"RFS host listening on port: {servingPort.Value}");

                    if (connectTo == null)
                    {
                        connectTo = $"http://localhost:{servingPort.Value}";
                    }
                }

                WindowsRfsClient? fs = null;
                FileSystemHost? host = null;
                if (path != null)
                {
                    fs = new WindowsRfsClient(
                        new WindowsRfs.WindowsRfsClient(
                            GrpcChannel.ForAddress(connectTo!)));
                    fs.AddAdditionalReparsePoints(junctions);
                    host = new FileSystemHost(fs);
                    var mountResult = host.Mount(Path.GetFullPath(path));
                    if (mountResult < 0)
                    {
                        _logger.LogError($"Failed to mount WinFsp filesystem: 0x{mountResult:X}");
                    }
                    _logger.LogInformation($"WinFsp mounted RFS to: {Path.GetFullPath(path)}");
                }

                try
                {
                    var semaphore = new SemaphoreSlim(0);
                    await semaphore.WaitAsync(context.GetCancellationToken());
                }
                catch
                {
                }

                if (host != null)
                {
                    host.Dispose();
                }

                if (app != null)
                {
                    await app.StopAsync();
                }

                return 0;
            }
        }
    }
}
