namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Abstractions;

    internal interface ICachedFile : IVfsFile
    {
        bool VerifyChunks(bool isFixing, Action<Action<PollingResponse>> updatePollingResponse);
    }
}
