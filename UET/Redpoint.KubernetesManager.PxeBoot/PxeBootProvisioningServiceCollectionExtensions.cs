namespace Redpoint.KubernetesManager.PxeBoot
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.Tpm;

    internal static class PxeBootProvisioningServiceCollectionExtensions
    {
        public static void AddPxeBootProvisioning(this IServiceCollection services)
        {
            services.AddTpm();

            services.AddSingleton<IProvisioningStep, TestProvisioningStep>();
            services.AddSingleton<IProvisioningStep, RegisterRemoteIpProvisioningStep>();
            services.AddSingleton<IProvisioningStep, RebootProvisioningStep>();
        }
    }
}
