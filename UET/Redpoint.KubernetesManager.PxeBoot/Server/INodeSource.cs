namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using System.Threading.Tasks;

    internal interface INodeSource
    {
        Task<bool> IsNodeAuthorizedAsync(CancellationToken cancellationToken);
    }
}
