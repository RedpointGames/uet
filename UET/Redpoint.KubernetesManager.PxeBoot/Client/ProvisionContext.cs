namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;

    internal class ProvisionContext
    {
        public required bool AllowRecoveryShell { get; init; }

        public required ProvisioningClientPlatformType Platform { get; init; }

        public required bool IsInRecovery { get; init; }

        public required string ApiAddress { get; init; }

        public required int BootedFromStepIndex { get; init; }
    }
}
