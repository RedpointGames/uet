namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using k8s;
    using k8s.Autorest;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The Kubernetes resources provisioning component creates the cluster
    /// roles and cluster role binding necessary for the API server to 
    /// directly communicate with kubelets running on nodes.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class KubernetesResourceProvisioningComponent : IComponent
    {
        private readonly ILogger<KubernetesResourceProvisioningComponent> _logger;

        public KubernetesResourceProvisioningComponent(
            ILogger<KubernetesResourceProvisioningComponent> logger)
        {
            _logger = logger;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for the Kubernetes API server to be available.
            var kubernetesContext = await context.WaitForFlagAsync<KubernetesClientContextData>(WellKnownFlags.KubeApiServerReady);
            var kubernetes = kubernetesContext.Kubernetes;

            _logger.LogInformation("Provisioning required resources inside Kubernetes...");

            // Provision the rbac.authorization.k8s.io/v1 ClusterRole that we will bind
            // to the API server, so that the API server can directly reach Kubelets on nodes.
            try
            {
                await kubernetes.RbacAuthorizationV1.ReadClusterRoleAsync("system:kube-apiserver-to-kubelet", cancellationToken: cancellationToken);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await kubernetes.RbacAuthorizationV1.CreateClusterRoleAsync(
                    new k8s.Models.V1ClusterRole
                    {
                        Metadata = new k8s.Models.V1ObjectMeta
                        {
                            Annotations = new Dictionary<string, string>
                           {
                               { "rbac.authorization.kubernetes.io/autoupdate", "true" },
                           },
                            Labels = new Dictionary<string, string>
                           {
                               { "kubernetes.io/bootstrapping", "rbac-defaults" },
                           },
                            Name = "system:kube-apiserver-to-kubelet"
                        },
                        Rules = new List<k8s.Models.V1PolicyRule>
                        {
                            new k8s.Models.V1PolicyRule
                            {
                                ApiGroups = new List<string> { string.Empty },
                                Resources = new List<string>
                                {
                                    "nodes/proxy",
                                    "nodes/stats",
                                    "nodes/log",
                                    "nodes/spec",
                                    "nodes/metrics",
                                },
                                Verbs = new List<string> { "*" },
                            }
                        }
                    },
                    cancellationToken: cancellationToken);
            }

            // Provision the rbac.authorization.k8s.io/v1 ClusterRoleBinding to actually
            // do the binding of the cluster role to the API server.
            try
            {
                await kubernetes.RbacAuthorizationV1.ReadClusterRoleBindingAsync("system:kube-apiserver", cancellationToken: cancellationToken);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await kubernetes.RbacAuthorizationV1.CreateClusterRoleBindingAsync(
                    new k8s.Models.V1ClusterRoleBinding
                    {
                        Metadata = new k8s.Models.V1ObjectMeta
                        {
                            Name = "system:kube-apiserver",
                            NamespaceProperty = string.Empty,
                        },
                        RoleRef = new k8s.Models.V1RoleRef
                        {
                            ApiGroup = "rbac.authorization.k8s.io",
                            Kind = "ClusterRole",
                            Name = "system:kube-apiserver-to-kubelet"
                        },
                        Subjects = new List<k8s.Models.Rbacv1Subject>
                        {
                            new k8s.Models.Rbacv1Subject
                            {
                                ApiGroup = "rbac.authorization.k8s.io",
                                Kind = "User",
                                Name = "kubernetes"
                            }
                        },
                    },
                    cancellationToken: cancellationToken);
            }

            context.SetFlag(WellKnownFlags.KubernetesResourcesProvisioned);
        }
    }
}
