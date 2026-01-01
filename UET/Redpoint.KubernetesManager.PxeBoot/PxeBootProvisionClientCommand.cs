namespace Redpoint.KubernetesManager.PxeBoot
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.Tpm;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Tpm;
    using System;
    using System.CommandLine;
    using System.Net;
    using System.Net.Http.Json;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class PxeBootProvisionClientCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<PxeBootProvisionClientOptions>()
            .WithInstance<PxeBootProvisionClientCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("provision-client", "Provision this client via PXE boot.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IParted, DefaultParted>();
                    services.AddPxeBootProvisioning();
                })
            .Build();
    }
}
