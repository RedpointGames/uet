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
    using System;
    using System.CommandLine;
    using System.Net;
    using System.Net.Http.Json;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class PxeBootInitrdBootstrapCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithInstance<PxeBootInitrdBootstrapCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("initrd-bootstrap", "Initialize this machine for booting Linux or Windows RKM nodes from PXE Boot.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddRkmTpm();
                    services.AddSingleton<IParted, DefaultParted>();
                })
            .Build();
    }
}
