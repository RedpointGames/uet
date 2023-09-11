namespace Redpoint.Uefs.Daemon.Abstractions
{
    using Redpoint.Uefs.Package;

    public class DockerVolume
    {
        public Concurrency.Semaphore Mutex = new Concurrency.Semaphore(1);
        public string Name = string.Empty;
        public string FilePath = string.Empty;
        public string? ContainerID;
        public string? Mountpoint;
        public IPackageMounter? PackageMounter;
    }
}
