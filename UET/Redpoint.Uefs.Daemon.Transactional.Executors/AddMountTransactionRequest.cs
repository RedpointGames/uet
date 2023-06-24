namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.State;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Threading.Tasks;

    public record class AddMountTransactionRequest : ITransactionRequest
    {
        public required string MountId { get; set; }

        public required string MountTypeDebugValue { get; set; }

        public required MountRequest MountRequest { get; set; }

        public required Func<CancellationToken, Task<(CurrentUefsMount mount, DaemonDatabasePersistentMount persistentMount)>> MountAsync { get; set; }

        public required bool IsBeingMountedOnStartup { get; set; }
    }
}
