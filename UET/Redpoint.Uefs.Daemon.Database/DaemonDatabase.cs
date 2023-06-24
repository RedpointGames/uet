namespace Redpoint.Uefs.Daemon.Database
{
    using System.Text.Json.Serialization;

    public class DaemonDatabase
    {
        public void AddPersistentMount(string mountPath, DaemonDatabasePersistentMount persistentMount)
        {
            if (_persistentMounts == null)
            {
                _persistentMounts = new Dictionary<string, DaemonDatabasePersistentMount>();
            }
            _persistentMounts[mountPath] = persistentMount;
        }

        public void RemovePersistentMount(string mountPath)
        {
            if (_persistentMounts == null)
            {
                _persistentMounts = new Dictionary<string, DaemonDatabasePersistentMount>();
            }
            if (_persistentMounts.ContainsKey(mountPath))
            {
                _persistentMounts.Remove(mountPath);
            }
        }

        public IReadOnlyDictionary<string, DaemonDatabasePersistentMount> GetPersistentMounts()
        {
            ConvertExistingPersistentMounts();
            if (_persistentMounts == null)
            {
                _persistentMounts = new Dictionary<string, DaemonDatabasePersistentMount>();
            }
            return _persistentMounts;
        }

        private void ConvertExistingPersistentMounts()
        {
            if (_persistentMountsLegacy != null && _persistentMounts == null)
            {
                _persistentMounts = _persistentMountsLegacy.ToDictionary(k => k.Key, v => new DaemonDatabasePersistentMount
                {
                    PackagePath = v.Value,
                    TagHint = null,
                    PersistenceMode = Protocol.WriteScratchPersistence.DiscardOnUnmount,
                });
                _persistentMountsLegacy = null;
            }
        }

        // mount path -> DaemonDatabasePersistentMount
        [JsonPropertyName("persistentMounts2")]
        private Dictionary<string, DaemonDatabasePersistentMount>? _persistentMounts = null;

        // mount path -> package path
        [JsonPropertyName("persistentMounts")]
        private Dictionary<string, string>? _persistentMountsLegacy = null;

        // mount path -> mount ID, used for restoring
        // IDs when the daemon starts and detects currently
        // mounted paths
        [JsonPropertyName("mountIdCache")]
        public Dictionary<string, string> MountIdCache { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    }
}
