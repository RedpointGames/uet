namespace UET.Commands.Internal.CMakeUbaServer
{
    using k8s.Models;

    internal class KubernetesNodeWorker
    {
        public V1Pod? KubernetesPod;
        public V1Service? KubernetesService;
        public int AllocatedCores;
        public string? UbaHost;
        public int? UbaPort;
    }
}
