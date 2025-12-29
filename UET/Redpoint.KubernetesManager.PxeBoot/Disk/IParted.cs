namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using System.Threading.Tasks;

    internal interface IParted
    {
        Task<string[]> GetDiskPathsAsync(CancellationToken cancellationToken);

        Task<PartedDisk> GetDiskAsync(string path, CancellationToken cancellationToken);

        Task RunCommandAsync(string diskPath, string[] args, CancellationToken cancellationToken);
    }
}
