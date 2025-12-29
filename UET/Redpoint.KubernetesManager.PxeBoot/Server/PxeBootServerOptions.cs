namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using System.CommandLine;

    internal class PxeBootServerOptions
    {
        public Option<string> DhcpOnInterface = new Option<string>(
            "--dhcp",
            "If specified, this command will run a DHCP server that will provide addresses to the network on the specified interface. This can be used to simplify testing of PXE boot with virtual machines.");
    }
}
