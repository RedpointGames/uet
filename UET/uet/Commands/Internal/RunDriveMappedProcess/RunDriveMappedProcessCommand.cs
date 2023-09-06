namespace UET.Commands.Internal.RunDriveMappedProcess
{
    using Fsp;
    using Google.Protobuf.Reflection;
    using Grpc.Net.Client;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
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

    internal class RunDriveMappedProcessCommand
    {
        internal class Options
        {
            public Option<string> ProcessPath;
            public Option<string> WorkingDirectory;
            public Option<string[]> DriveMappings;
            public Option<string[]> Arguments;
            public Option<string[]> ArgumentsAt;
            public Option<string[]> EnvironmentVariables;
            public Option<string> Rfs;

            public Options()
            {
                ProcessPath = new Option<string>("--process-path");
                WorkingDirectory = new Option<string>("--working-directory");
                DriveMappings = new Option<string[]>("--drive-map");
                Arguments = new Option<string[]>("--arg");
                ArgumentsAt = new Option<string[]>("--arg-at");
                EnvironmentVariables = new Option<string[]>("--env");
                Rfs = new Option<string>("--rfs");
            }
        }

        public static Command CreateRunDriveMappedProcessCommand()
        {
            var options = new Options();
            var command = new Command("run-drive-mapped-process");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunDriveMappedProcessCommandInstance>(options);
            return command;
        }

        private class RunDriveMappedProcessCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunDriveMappedProcessCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public RunDriveMappedProcessCommandInstance(
                ILogger<RunDriveMappedProcessCommandInstance> logger,
                IProcessExecutor processExecutor,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                {
                    _logger.LogError("OS not supported.");
                    return 1;
                }

                var processPath = context.ParseResult.GetValueForOption(_options.ProcessPath) ?? @"C:\Windows\system32\cmd.exe";
                var workingDirectory = context.ParseResult.GetValueForOption(_options.WorkingDirectory);
                var driveMappings = context.ParseResult.GetValueForOption(_options.DriveMappings);
                var arguments = context.ParseResult.GetValueForOption(_options.Arguments) ?? Array.Empty<string>();
                var argumentsAt = context.ParseResult.GetValueForOption(_options.ArgumentsAt) ?? Array.Empty<string>();
                var envVars = context.ParseResult.GetValueForOption(_options.EnvironmentVariables) ?? Array.Empty<string>();
                var rfs = context.ParseResult.GetValueForOption(_options.Rfs);

                WebApplication? app = null;
                WindowsRfsClient? fs = null;
                FileSystemHost? host = null;
                if (rfs != null)
                {
                    rfs = Path.GetFullPath(rfs);

                    var builder = WebApplication.CreateBuilder();
                    builder.Logging.ClearProviders();
                    builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
                    builder.Services.AddGrpc(options =>
                    {
                        // Allow unlimited message sizes.
                        options.MaxReceiveMessageSize = null;
                        options.MaxSendMessageSize = null;
                    });
                    builder.Services.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(
                        typeof(WindowsRfs.WindowsRfsBase),
                        new WindowsRfsHost(_logger)));
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Listen(
                            new IPEndPoint(IPAddress.Loopback, 0),
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

                    var servingPort = new Uri(app.Urls.First()).Port;

                    fs = new WindowsRfsClient(
                        _logger,
                        new WindowsRfs.WindowsRfsClient(
                            GrpcChannel.ForAddress($"http://localhost:{servingPort}")));
                    //fs.AddAdditionalReparsePoints(junctions);
                    host = new FileSystemHost(fs);
                    var mountResult = host.Mount(rfs);
                    if (mountResult < 0)
                    {
                        _logger.LogError($"Failed to mount WinFsp filesystem: 0x{mountResult:X}");
                        return 1;
                    }
                }

                var envVarsDict = new Dictionary<string, string>();
                foreach (string key in Environment.GetEnvironmentVariables().Keys)
                {
                    envVarsDict[key] = Environment.GetEnvironmentVariable(key)!;
                }
                foreach (var entry in envVars)
                {
                    var kv = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                    envVarsDict[kv[0]] = kv[1];
                }

                var spec = new ProcessSpecification
                {
                    FilePath = processPath,
                    Arguments = arguments.Concat(argumentsAt.Select(x => '@' + x)).ToArray(),
                    WorkingDirectory = workingDirectory,
                    EnvironmentVariables = envVarsDict,
                };
                if (driveMappings != null && OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
                {
                    if (rfs != null)
                    {
                        spec.PerProcessDriveMappings = DriveInfo
                            .GetDrives()
                                .Where(x => Path.Exists(Path.Combine(rfs, x.Name[0].ToString())))
                                .ToDictionary(
                                    k => k.Name[0],
                                            v => Path.Combine(rfs, v.Name[0].ToString()));
                    }
                    else
                    {
                        spec.PerProcessDriveMappings = new Dictionary<char, string>();
                        foreach (var mapping in driveMappings)
                        {
                            var c = mapping.Split('=', 2, StringSplitOptions.TrimEntries);
                            spec.PerProcessDriveMappings[c[0][0]] = c[1];

                            if (c[0].ToUpperInvariant()[0] == 'C' &&
                                !Directory.Exists(Path.Combine(c[1], "Windows")))
                            {
                                var systemRoot = Environment.GetEnvironmentVariable("SYSTEMROOT")!;
                                if (Directory.Exists(systemRoot))
                                {
                                    Junction.CreateJunction(
                                        Path.Combine(c[1], "Windows"),
                                        systemRoot,
                                        true);
                                }
                            }
                        }
                    }
                }

                var exitCode = await _processExecutor.ExecuteAsync(
                    spec,
                    CaptureSpecification.Passthrough,
                    CancellationToken.None);
                return exitCode;
            }
        }
    }
}
