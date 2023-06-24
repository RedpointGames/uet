namespace Redpoint.Uefs.Daemon.RemoteStorage.Registry
{
    using Docker.Registry.DotNet.Models;
    using Redpoint.Uefs.Daemon.RemoteStorage;

    public class RegistryRemoteStorage : IRemoteStorage<ManifestLayer>
    {
        public string Type => "registry";

        public IRemoteStorageBlobFactory GetFactory(ManifestLayer reference)
        {
            throw new InvalidOperationException("Pulling stored packages from the registry is not supported yet. Please try using a package reference instead.");
        }
    }
}
