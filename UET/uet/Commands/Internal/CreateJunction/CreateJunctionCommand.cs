namespace UET.Commands.Internal.CreateJunction
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class CreateJunctionCommand
    {
        internal class Options
        {
            public Option<DirectoryInfo> LinkPath;
            public Option<DirectoryInfo?> TargetPath;
            public Option<string?> TargetRaw;

            public Options()
            {
                LinkPath = new Option<DirectoryInfo>("--link-path");
                TargetPath = new Option<DirectoryInfo?>("--target-path");
                TargetRaw = new Option<string?>("--target-raw");
            }
        }

        public static Command CreateCreateJunctionCommand()
        {
            var options = new Options();
            var command = new Command("create-junction");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateJunctionCommandInstance>(options);
            return command;
        }

        private class CreateJunctionCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateJunctionCommandInstance> _logger;
            private readonly Options _options;

            public CreateJunctionCommandInstance(
                ILogger<CreateJunctionCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
                {
                    _logger.LogError("This command is not supported on this operating system.");
                    return Task.FromResult(1);
                }

                var linkPath = context.ParseResult.GetValueForOption(_options.LinkPath)!;
                var targetPath = context.ParseResult.GetValueForOption(_options.TargetPath);
                var targetRaw = context.ParseResult.GetValueForOption(_options.TargetRaw);

                if (targetPath != null)
                {
                    Junction.CreateJunction(
                        linkPath.FullName,
                        targetPath.FullName,
                        true);
                }
                else if (targetRaw != null)
                {
                    Junction.CreateRawJunction(
                        linkPath.FullName,
                        targetRaw,
                        true);
                }
                else
                {
                    _logger.LogError("You must pass --target-path or --target-raw.");
                    return Task.FromResult(1);
                }

                return Task.FromResult(0);
            }
        }
    }
}
