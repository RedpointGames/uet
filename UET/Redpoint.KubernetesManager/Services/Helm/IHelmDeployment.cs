namespace Redpoint.KubernetesManager.Services.Helm
{
    using k8s.KubeConfigModels;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IHelmDeployment
    {
        Task<int> DeployChart(
            IContext context,
            string chartName,
            string ociUrl,
            string valuesContent,
            bool waitForResourceStabilisation,
            CancellationToken cancellationToken);
    }
}
