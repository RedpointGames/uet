namespace Redpoint.Uefs.Daemon.Abstractions
{
    using Redpoint.Uefs.Daemon.PackageStorage;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;

    public interface IUefsDaemon : IMountTracking, IAsyncDisposable
    {
        Task StartAsync();

        string StoragePath { get; }

        Dictionary<string, DockerVolume> DockerVolumes { get; }

        IPackageStorage PackageStorage { get; }

        ITransactionalDatabase TransactionalDatabase { get; }
    }
}
