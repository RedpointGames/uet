namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using System.CommandLine;

    internal class PxeBootProvisionClientOptions
    {
        public Option<bool> Local = new Option<bool>("--local", "Assume the provisioning API is running locally for testing.");
    }
}
