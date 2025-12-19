namespace UET.Commands.Internal.Patch
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class PatchCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<PatchCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("patch");
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> EnginePath;

            public Options()
            {
                EnginePath = new Option<string>("--engine-path");
            }
        }

        private sealed class PatchCommandInstance : ICommandInstance
        {
            private readonly IBuildGraphPatcher _patcher;
            private readonly Options _options;

            public PatchCommandInstance(
                IBuildGraphPatcher patcher,
                Options options)
            {
                _patcher = patcher;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                await _patcher.PatchBuildGraphAsync(
                    context.ParseResult.GetValueForOption(_options.EnginePath)!,
                    false);
                return 0;
            }
        }
    }
}
