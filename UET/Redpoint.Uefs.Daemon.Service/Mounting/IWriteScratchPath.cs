namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Redpoint.Uefs.Protocol;

    internal interface IWriteScratchPath
    {
        string ComputeWriteScratchPath(MountRequest request, string targetPath);
    }
}
