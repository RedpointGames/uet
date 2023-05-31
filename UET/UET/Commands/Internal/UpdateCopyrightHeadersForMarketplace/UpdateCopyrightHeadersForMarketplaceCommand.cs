namespace UET.Commands.Internal.UpdateCopyrightHeadersForMarketplace
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

    internal class UpdateCopyrightHeadersForMarketplaceCommand
    {
        internal class Options
        {
            public Option<string> Path;
            public Option<string> CopyrightHeader;
            public Option<string> CopyrightExcludes;

            public Options()
            {
                Path = new Option<string>("--path");
                CopyrightHeader = new Option<string>("--copyright-header");
                CopyrightExcludes = new Option<string>("--copyright-excludes");
            }
        }

        public static Command CreateUpdateCopyrightHeadersForMarketplaceCommand()
        {
            var options = new Options();
            var command = new Command("update-copyright-headers-for-marketplace");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpdateCopyrightHeadersForMarketplaceCommandInstance>(options);
            return command;
        }

        private class UpdateCopyrightHeadersForMarketplaceCommandInstance : ICommandInstance
        {
            private readonly ILogger<UpdateCopyrightHeadersForMarketplaceCommandInstance> _logger;

            public UpdateCopyrightHeadersForMarketplaceCommandInstance(
                ILogger<UpdateCopyrightHeadersForMarketplaceCommandInstance> logger)
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
