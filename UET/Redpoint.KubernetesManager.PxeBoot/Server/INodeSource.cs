namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface INodeSource
    {
        Task<bool> IsNodeAuthorizedAsync(CancellationToken cancellationToken);
    }
}
