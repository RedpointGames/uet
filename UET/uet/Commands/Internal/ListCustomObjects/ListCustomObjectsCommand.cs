namespace UET.Commands.Internal.ListCustomObjects
{
    using k8s;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    internal sealed class ListCustomObjectsCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<ListRkmNodesCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("list-custom-objects");
                })
            .Build();

        private sealed class ListRkmNodesCommandInstance : ICommandInstance
        {
            private readonly ILogger<ListRkmNodesCommandInstance> _logger;

            public ListRkmNodesCommandInstance(
                ILogger<ListRkmNodesCommandInstance> logger)
            {
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var client = new Kubernetes(
                    KubernetesClientConfiguration.BuildDefaultConfig());

                var result = await client.CustomObjects.ListClusterCustomObjectAsync(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmconfigurations",
                    cancellationToken: context.GetCancellationToken());

                _logger.LogInformation($"{result.GetType().FullName} {result}");

                return 0;
            }
        }
    }
}
