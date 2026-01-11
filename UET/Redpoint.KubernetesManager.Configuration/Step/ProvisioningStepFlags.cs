namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [Flags]
    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This is a flags enumeration.")]
    public enum ProvisioningStepFlags
    {
        None = 0,

        DoNotStartAutomaticallyNextStepOnCompletion = 0x1,

        AssumeCompleteWhenIpxeScriptFetched = 0x2,

        CommitOnCompletion = 0x4,

        SetAsRebootStepIndex = 0x8,

        DisallowInAtomicSequence = 0x10,
    }
}
