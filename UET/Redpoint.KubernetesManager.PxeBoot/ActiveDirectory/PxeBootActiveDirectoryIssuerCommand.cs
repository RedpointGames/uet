namespace Redpoint.KubernetesManager.PxeBoot.ActiveDirectory
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.KubernetesManager.PxeBoot.Server.Handlers;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Net.Http.Headers;

    internal class PxeBootActiveDirectoryIssuerCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<PxeBootActiveDirectoryIssuerOptions>()
            .WithInstance<PxeBootActiveDirectoryIssuerCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("active-directory-issuer", "Run the Active Directory issuer on this machine, which provides offline join data for machines when those machines are authorized in the cluster.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm-ad-issuer";
                        });
                    }
                    services.AddRkmHostedServiceEnvironment("rkm-ad-issuer");
                    services.AddHostedService<PxeBootActiveDirectoryIssuerHostedService>();
                    services.AddKestrelFactory();
                    services.AddTpm();
                })
            .Build();
    }
}
