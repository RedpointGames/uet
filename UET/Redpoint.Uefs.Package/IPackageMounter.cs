namespace Redpoint.Uefs.Package
{
    using Redpoint.Uefs.Protocol;

    public interface IPackageMounter : IAsyncDisposable
    {
        static Memory<byte> MagicHeader { get; }

        bool RequiresAdminPermissions { get; }

        bool CompatibleWithDocker { get; }

        string? WriteStoragePath { get; }

        ValueTask MountAsync(
            string packagePath,
            string mountPath,
            string writeStoragePath,
            WriteScratchPersistence persistenceMode);

        Task<(string packagePath, string mountPath, IPackageMounter mounter)[]> ImportExistingMountsAtStartupAsync();
    }
}
