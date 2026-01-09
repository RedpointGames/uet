namespace Redpoint.KubernetesManager.PxeBoot
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.DeleteBootLoaderEntry;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RecoveryShell;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Sequence;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using Redpoint.Tpm;

    internal static class PxeBootProvisioningServiceCollectionExtensions
    {
        public static void AddPxeBootProvisioning(this IServiceCollection services)
        {
            services.AddTpm();

            services.AddSingleton<IProvisioningStep, TestProvisioningStep>();
            services.AddSingleton<IProvisioningStep, RegisterRemoteIpProvisioningStep>();
            services.AddSingleton<IProvisioningStep, RebootProvisioningStep>();
            services.AddSingleton<IProvisioningStep, RecoveryShellProvisioningStep>();
            services.AddSingleton<IProvisioningStep, ExecuteProcessProvisioningStep>();
            services.AddSingleton<IProvisioningStep, AtomicSequenceProvisioningStep>();
            services.AddSingleton<IProvisioningStep, UploadFilesProvisioningStep>();
            services.AddSingleton<IProvisioningStep, ModifyFilesProvisioningStep>();
            services.AddSingleton<IProvisioningStep, DeleteBootLoaderEntryProvisioningStep>();
            services.AddSingleton<IProvisioningStep, InitializeOsPartitionProvisioningStep>();

            services.AddSingleton<IDurableOperation, DefaultDurableOperation>();
            services.AddSingleton<IFileTransferClient, DefaultFileTransferClient>();

            services.AddSingleton<IVariableProvider, DefaultVariableProvider>();

            services.AddSingleton<IReboot, DefaultReboot>();

            services.AddSingleton<IProvisionContextDiscoverer, DefaultProvisionContextDiscoverer>();

            services.AddSingleton<IParted, DefaultParted>();
            services.AddSingleton<IOperatingSystemPartitionManager, DefaultOperatingSystemPartitionManager>();
        }
    }
}
