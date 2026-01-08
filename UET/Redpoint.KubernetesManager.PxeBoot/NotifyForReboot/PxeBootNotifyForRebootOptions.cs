namespace Redpoint.KubernetesManager.PxeBoot.NotifyForReboot
{
    using System.CommandLine;

    internal class PxeBootNotifyForRebootOptions
    {
        public Option<string> Fingerprint = new Option<string>("--fingerprint");
    }
}
