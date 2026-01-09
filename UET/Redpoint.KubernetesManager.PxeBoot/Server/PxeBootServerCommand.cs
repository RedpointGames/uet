namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using GitHub.JPMikkers.Dhcp;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Server.Handlers;
    using System.CommandLine;

    internal class PxeBootServerCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithInstance<PxeBootServerCommandInstance>()
            .WithOptions<PxeBootServerOptions>()
            .WithCommand(
                builder =>
                {
                    return new Command("server", "Runs the server that serves PXE boot requests and files for provisioning machines.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddRkmHostedServiceEnvironment("rkm-pxeboot");
                    services.AddHostedService<PxeBootHostedService>();
                    services.AddDhcpServer();
                    services.AddKestrelFactory();
                    services.AddPxeBootProvisioning();

                    services.AddSingleton<IFileTransferServer, DefaultFileTransferServer>();

                    services.AddSingleton<IPxeBootHttpRequestHandler, DefaultPxeBootHttpRequestHandler>();
                    services.AddSingleton<IPxeBootTftpRequestHandler, DefaultPxeBootTftpRequestHandler>();

                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, IpxeUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, AutoexecUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, StaticUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, IpxeUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, UploadedUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, RkmProvisionContextUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, NotifyForRebootUnauthenticatedFileTransferEndpoint>();
                    services.AddSingleton<IUnauthenticatedFileTransferEndpoint, WinPeUnauthenticatedFileTransferEndpoint>();

                    services.AddSingleton<INodeProvisioningEndpoint, AuthorizeNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, RebootToDiskNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, StepNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, StepCompleteNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, ForceReprovisionFromRecoveryNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, UploadFileNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, SyncBootEntriesProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, QueryRebootRequiredNodeProvisioningEndpoint>();
                    services.AddSingleton<INodeProvisioningEndpoint, QueryServicesNodeProvisioningEndpoint>();

                    services.AddSingleton<IProvisionerHasher, DefaultProvisionerHasher>();
                })
            .Build();
    }
}
