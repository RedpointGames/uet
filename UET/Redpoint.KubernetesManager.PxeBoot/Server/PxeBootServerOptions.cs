namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using System.CommandLine;
    using System.Net;

    internal class PxeBootServerOptions
    {
        public Option<string> DhcpOnInterface = new Option<string>(
            "--dhcp",
            "If specified, this command will run a DHCP server that will provide addresses to the network on the specified interface. This can be used to simplify testing of PXE boot with virtual machines.");

        public Option<PxeBootServerSource> Source = new Option<PxeBootServerSource>(
            "--source",
            () => PxeBootServerSource.Test,
            "The configuration source.");

        public Option<IPAddress> HostAddress = new Option<IPAddress>(
            "--host-address",
            "The address that the PXE boot server is listening on; used to notify machines of the API address.");

        public Option<DirectoryInfo> StaticFiles = new Option<DirectoryInfo>(
            "--static-files",
            () => new DirectoryInfo("/static"),
            "The directory that contains the static delivery files.");

        public Option<DirectoryInfo> StorageFiles = new Option<DirectoryInfo>(
            "--storage-files",
            () => new DirectoryInfo("/storage"),
            "The directory under which to store dynamic, temporary data from provisioning nodes.");
    }
}
