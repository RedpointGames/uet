namespace Redpoint.KubernetesManager.PxeBoot.ActiveDirectory
{
    using System.CommandLine;

    internal class PxeBootActiveDirectoryIssuerOptions
    {
        public Option<bool> Install = new Option<bool>("--install");

        public Option<string> Domain = new Option<string>("--domain") { IsRequired = true };

        public Option<string> Username = new Option<string>("--username");

        public Option<string> Password = new Option<string>("--password");

        public Option<string> ProvisionerApiAddress = new Option<string>("--provisioner-api-address") { IsRequired = true };
    }
}
