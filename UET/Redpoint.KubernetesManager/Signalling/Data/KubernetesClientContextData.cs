namespace Redpoint.KubernetesManager.Signalling.Data
{
    using k8s;

    internal class KubernetesClientContextData : IAssociatedData
    {
        public KubernetesClientContextData(IKubernetes kubernetes)
        {
            Kubernetes = kubernetes;
        }

        public IKubernetes Kubernetes { get; }
    }
}
