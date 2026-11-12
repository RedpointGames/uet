using Redpoint.Uet.CommonPaths;

namespace UET.Commands.Internal.CheckSlowDrive
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Text;

    internal sealed class CheckSlowDriveCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<CheckSlowDriveCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("check-slow-drive", "Checks if UEFS is storing files on a slow external drive.");
                })
            .Build();


        private sealed class CheckSlowDriveCommandInstance : ICommandInstance
        {
            private readonly ILogger<CheckSlowDriveCommandInstance> _logger;

            public CheckSlowDriveCommandInstance(
                ILogger<CheckSlowDriveCommandInstance> logger)
            {
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                UetPaths.InitUefsRootPath(x => _logger.LogInformation(x));
                if (UetPaths.UefsIsOnSlowDrive)
                {
                    _logger.LogWarning("UEFS is writing package images to a very slow external USB drive. Please plug the external drive into a faster port, otherwise image transfers will be extremely slow.");
                    return 1;
                }
                else
                {
                    _logger.LogInformation("Slow drive not detected.");
                    return 0;
                }
            }
        }
    }
}