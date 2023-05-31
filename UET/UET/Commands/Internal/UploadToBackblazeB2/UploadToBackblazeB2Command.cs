namespace UET.Commands.Internal.UploadToBackblazeB2
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

    internal class UploadToBackblazeB2Command
    {
        internal class Options
        {
            public Option<string> ZipPath;
            public Option<string> BucketName;
            public Option<string> FolderEnvVar;
            public Option<bool> Marketplace;

            public Options()
            {
                ZipPath = new Option<string>("--zip-path");
                BucketName = new Option<string>("--bucket-name");
                FolderEnvVar = new Option<string>("--folder-env-var");
                Marketplace = new Option<bool>("--marketplace");
            }
        }

        public static Command CreateUploadToBackblazeB2Command()
        {
            var options = new Options();
            var command = new Command("upload-to-backblaze-b2", "Uploads a ZIP file to Backblaze B2.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UploadToBackblazeB2CommandInstance>(options);
            return command;
        }

        private class UploadToBackblazeB2CommandInstance : ICommandInstance
        {
            private readonly ILogger<UploadToBackblazeB2CommandInstance> _logger;

            public UploadToBackblazeB2CommandInstance(
                ILogger<UploadToBackblazeB2CommandInstance> logger)
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
