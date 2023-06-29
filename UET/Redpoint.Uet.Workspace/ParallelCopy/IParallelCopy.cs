namespace Redpoint.Uet.Workspace.ParallelCopy
{
    using System.Threading.Tasks;

    internal interface IParallelCopy
    {
        Task CopyAsync(CopyDescriptor descriptor, CancellationToken cancellationToken);
    }
}
