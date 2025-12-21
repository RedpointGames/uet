namespace Redpoint.KubernetesManager.Signalling.Data
{
    using k8s;

    internal class KubernetesClientContextData : IAssociatedData
    {
        public KubernetesClientContextData(
            IKubernetes kubernetes,
            string kubeconfigData)
        {
            Kubernetes = kubernetes;
            KubeconfigData = kubeconfigData;
        }

        public IKubernetes Kubernetes { get; }

        public string KubeconfigData { get; }
    }
}
