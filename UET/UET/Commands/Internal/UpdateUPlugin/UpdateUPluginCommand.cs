namespace UET.Commands.Internal.UpdateUPlugin
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class UpdateUPluginCommand
    {
        internal class Options
        {
            public Option<string> InputPath;
            public Option<string> OutputPath;
            public Option<string> EngineVersion;
            public Option<string> VersionName;
            public Option<string> VersionNumber;
            public Option<bool> Marketplace;

            public Options()
            {
                InputPath = new Option<string>("--input-path");
                OutputPath = new Option<string>("--output-path");
                EngineVersion = new Option<string>("--engine-version");
                VersionName = new Option<string>("--version-name");
                VersionNumber = new Option<string>("--version-number");
                Marketplace = new Option<bool>("--marketplace");
            }
        }

        public static Command CreateUpdateUPluginCommand()
        {
            var options = new Options();
            var command = new Command("update-uplugin");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpdateUPluginCommandInstance>(options);
            return command;
        }

        private class UpdateUPluginCommandInstance : ICommandInstance
        {
            private readonly ILogger<UpdateUPluginCommandInstance> _logger;

            public UpdateUPluginCommandInstance(
                ILogger<UpdateUPluginCommandInstance> logger)
            {
                _logger = logger;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                _logger.LogError("Not yet implemented.");
                return Task.FromResult(1);
            }
        }
    }
}
