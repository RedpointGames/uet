namespace UET.Commands.Internal.DumpEnvironment
{
    using Fractions.Extensions;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class DumpEnvironmentCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<DumpEnvironmentCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("dump-environment");
                })
            .Build();

        private sealed class DumpEnvironmentCommandInstance : ICommandInstance
        {
            private readonly ILogger<DumpEnvironmentCommandInstance> _logger;

            public DumpEnvironmentCommandInstance(
                ILogger<DumpEnvironmentCommandInstance> logger)
            {
                _logger = logger;
            }

            public Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                EnvironmentVariableTarget[] targets = OperatingSystem.IsWindows()
                    ? [EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine]
                    : [EnvironmentVariableTarget.Process];
                foreach (var target in targets)
                {
                    _logger.LogInformation($"Inspecting environment variable target {target}...");
                    _logger.LogInformation($"  PATH: {Environment.GetEnvironmentVariable("PATH")}");
                    _logger.LogInformation($"  PATHEXT: {Environment.GetEnvironmentVariable("PATHEXT")}");

                    var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                    var pathExts = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) : [];

                    _logger.LogInformation($"  PATH (array):");
                    foreach (var v in paths)
                    {
                        _logger.LogInformation($"    {v}");
                    }
                    _logger.LogInformation($"  PATHEXT (array):");
                    foreach (var v in pathExts)
                    {
                        _logger.LogInformation($"    {v}");
                    }

                    _logger.LogInformation("Searching for 'pwsh'...");
                    var binaryName = "pwsh";
                    foreach (var path in paths)
                    {
                        if (pathExts.Length > 0)
                        {
                            foreach (var pathExt in pathExts)
                            {
                                var fullPath = Path.Combine(path, $"{binaryName}{pathExt.ToLowerInvariant()}");
                                var fullPathExists = File.Exists(fullPath);
                                if (fullPathExists)
                                {
                                    _logger.LogInformation($"  {fullPath} (exists)");
                                }
                                else
                                {
                                    _logger.LogWarning($"  {fullPath} (not found)");
                                }
                            }
                        }
                        else
                        {
                            var fullPath = Path.Combine(path, binaryName);
                            var fullPathExists = File.Exists(fullPath);
                            if (fullPathExists)
                            {
                                _logger.LogInformation($"  {fullPath} (exists)");
                            }
                            else
                            {
                                _logger.LogWarning($"  {fullPath} (not found)");
                            }
                        }
                    }
                }

                return Task.FromResult(0);
            }
        }
    }
}
