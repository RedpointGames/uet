namespace Redpoint.KubernetesManager.PxeBoot.Provisioning
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    internal interface IProvisionerHasher
    {
        string GetProvisionerHash(
            ServerSideVariableContext context);
    }
}
