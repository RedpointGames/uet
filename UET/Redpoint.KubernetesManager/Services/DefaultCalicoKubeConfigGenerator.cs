namespace Redpoint.KubernetesManager.Services
{
    using k8s;
    using k8s.Autorest;
    using k8s.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultCalicoKubeConfigGenerator : ICalicoKubeConfigGenerator
    {
        private readonly ILogger<DefaultCalicoKubeConfigGenerator> _logger;
        private readonly ICertificateManager _certificateManager;
        private readonly IPathProvider _pathProvider;

        public DefaultCalicoKubeConfigGenerator(
            ILogger<DefaultCalicoKubeConfigGenerator> logger,
            ICertificateManager certificateManager,
            IPathProvider pathProvider)
        {
            _logger = logger;
            _certificateManager = certificateManager;
            _pathProvider = pathProvider;
        }

        public async Task<string> ProvisionCalicoKubeConfigIfNeededAsync(IKubernetes kubernetes, CancellationToken stoppingToken)
        {
            var kubeconfigsPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs");
            if (File.Exists(Path.Combine(kubeconfigsPath, "components", "component-calico-windows.kubeconfig")))
            {
                return await File.ReadAllTextAsync(Path.Combine(kubeconfigsPath, "components", "component-calico-windows.kubeconfig"), stoppingToken);
            }

            // First, get the calico-node-token service account that lives
            // in the kube-system namespace. It might not exist, and if it doesn't
            // we'll need to ask Kubernetes to create it.
            V1Secret? secret;
            try
            {
                secret = await kubernetes.CoreV1.ReadNamespacedSecretAsync("calico-node-token", "kube-system", cancellationToken: stoppingToken);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // We have to create the secret.
                secret = await kubernetes.CoreV1.CreateNamespacedSecretAsync(
                    new V1Secret
                    {
                        ApiVersion = "v1",
                        Kind = "Secret",
                        Metadata = new V1ObjectMeta
                        {
                            Name = "calico-node-token",
                            NamespaceProperty = "kube-system",
                            Annotations = new Dictionary<string, string>
                            {
                                { "kubernetes.io/service-account.name", "calico-node" },
                            },
                        },
                        Type = "kubernetes.io/service-account-token"
                    },
                    "kube-system",
                    cancellationToken: stoppingToken);
            }

            var certificateAuthorityPem = await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath("ca", "ca"), stoppingToken);

            while ((secret.Data == null ||
                !secret.Data.ContainsKey("token") ||
                secret.Data["token"] == null) &&
                !stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Waiting for Kubernetes to provision a token for the calico-node-token secret...");
                await Task.Delay(1000, stoppingToken);
                secret = await kubernetes.CoreV1.ReadNamespacedSecretAsync("calico-node-token", "kube-system", cancellationToken: stoppingToken);
            }
            stoppingToken.ThrowIfCancellationRequested();

            var kubeconfig = $@"
apiVersion: v1
kind: Config
clusters:
- name: kubernetes
  cluster:
    server: https://__CONTROLLER_ADDRESS__:6443
    certificate-authority-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(certificateAuthorityPem))}
contexts:
- name: calico-windows@kubernetes
  context:
    cluster: kubernetes
    namespace: kube-system
    user: calico-windows
current-context: calico-windows@kubernetes
users:
- name: calico-windows
  user:
    token: {Encoding.ASCII.GetString(secret.Data!["token"])}
".Trim();
            await File.WriteAllTextAsync(Path.Combine(kubeconfigsPath, "components", "component-calico-windows.kubeconfig"), kubeconfig, stoppingToken);
            return kubeconfig;
        }
    }
}
