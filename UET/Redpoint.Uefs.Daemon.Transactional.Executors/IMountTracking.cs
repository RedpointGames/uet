namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.State;
    using System.Threading.Tasks;

    public interface IMountTracking
    {
        IReadOnlyDictionary<string, CurrentUefsMount> CurrentMounts { get; }

        bool IsPathMountPath(string path);

        Task AddCurrentMountAsync(string id, CurrentUefsMount mount);

        Task RemoveCurrentMountAsync(string id);

        Task AddPersistentMountAsync(string mountPath, DaemonDatabasePersistentMount persistentMount);

        Task RemovePersistentMountAsync(string mountPath);
    }
}
