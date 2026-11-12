namespace Redpoint.Uet.Uefs.InProcess
{
    using Redpoint.Uefs.Protocol;

    public interface IUefsInProcessManager
    {
        Task<string> PullTagToPackagePathAsync(MountPackageTagRequest request, CancellationToken cancellationToken);
    }
}
