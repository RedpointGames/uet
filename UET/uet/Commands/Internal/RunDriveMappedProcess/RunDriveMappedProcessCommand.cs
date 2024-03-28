namespace UET.Commands.Internal.RunDriveMappedProcess
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Threading.Tasks;

    internal sealed class RunDriveMappedProcessCommand
    {
        internal sealed class Options
        {
            public Option<string> ProcessPath;
            public Option<string> WorkingDirectory;
            public Option<string[]> DriveMappings;
            public Option<string[]> Arguments;
            public Option<string[]> ArgumentsAt;
            public Option<string[]> EnvironmentVariables;

            public Options()
            {
                ProcessPath = new Option<string>("--process-path");
                WorkingDirectory = new Option<string>("--working-directory");
                DriveMappings = new Option<string[]>("--drive-map");
                Arguments = new Option<string[]>("--arg");
                ArgumentsAt = new Option<string[]>("--arg-at");
                EnvironmentVariables = new Option<string[]>("--env");
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

        private sealed class RunDriveMappedProcessCommandInstance : ICommandInstance
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
                    var newPerProcessDriveMappings = new Dictionary<char, string>();
                    foreach (var mapping in driveMappings)
                    {
                        var c = mapping.Split('=', 2, StringSplitOptions.TrimEntries);
                        newPerProcessDriveMappings[c[0][0]] = c[1];

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
                    spec.PerProcessDriveMappings = newPerProcessDriveMappings;
                }

                var exitCode = await _processExecutor.ExecuteAsync(
                    spec,
                    CaptureSpecification.Passthrough,
                    CancellationToken.None).ConfigureAwait(false);
                return exitCode;
            }
        }
    }
}
