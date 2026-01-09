namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using System.CommandLine;

    internal class PxeBootMonitorClientOptions
    {
        public Option<bool> Install = new Option<bool>("--install");

        public Option<string> ProvisionerApiAddress = new Option<string>("--provisioner-api-address") { IsRequired = true };
    }
}
