namespace UET.Commands.Internal.CopyAndMutateBuildCs
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

    internal class CopyAndMutateBuildCsCommand
    {
        internal class Options
        {
            public Option<string> InputBasePath;
            public Option<string> InputFileList;
            public Option<string> OutputPath;
            public Option<bool> Marketplace;

            public Options()
            {
                InputBasePath = new Option<string>("--input-base-path");
                InputFileList = new Option<string>("--input-file-list");
                OutputPath = new Option<string>("--output-path");
                Marketplace = new Option<bool>("--marketplace");
            }
        }

        public static Command CreateCopyAndMutateBuildCsCommand()
        {
            var options = new Options();
            var command = new Command("copy-and-mutate-build-cs");
            command.AddAllOptions(options);
            command.AddCommonHandler<CopyAndMutateBuildCsCommandInstance>(options);
            return command;
        }

        private class CopyAndMutateBuildCsCommandInstance : ICommandInstance
        {
            private readonly ILogger<CopyAndMutateBuildCsCommandInstance> _logger;

            public CopyAndMutateBuildCsCommandInstance(
                ILogger<CopyAndMutateBuildCsCommandInstance> logger)
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
