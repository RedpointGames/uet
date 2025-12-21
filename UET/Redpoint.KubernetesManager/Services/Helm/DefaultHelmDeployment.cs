namespace Redpoint.KubernetesManager.Services.Helm
{
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using Redpoint.ProcessExecution;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class DefaultHelmDeployment : IHelmDeployment
    {
        private readonly IPathProvider _pathProvider;
        private readonly IProcessExecutor _processExecutor;

        public DefaultHelmDeployment(
            IPathProvider pathProvider,
            IProcessExecutor processExecutor)
        {
            _pathProvider = pathProvider;
            _processExecutor = processExecutor;
        }

        public async Task<int> DeployChart(
            IContext context,
            string chartName,
            string ociUrl,
            string valuesContent,
            bool waitForResourceStabilisation,
            CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.KubeconfigsReady);

            // Wait for the Kubernetes API server to be available.
            var kubernetesContext = await context.WaitForFlagAsync<KubernetesClientContextData>(WellKnownFlags.KubeApiServerReady);
            var kubernetes = kubernetesContext.Kubernetes;
            var kubeconfigData = kubernetesContext.KubeconfigData;

            // The path to Helm that we extracted earlier.
            var helmPath = Path.Combine(_pathProvider.RKMRoot, "helm-bin", "helm");

            // Generate the values.yaml file for our deployment, since we can't reliably set plugin parameters
            // via --set on the command line.
            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "helm-values"));
            var valuesPath = Path.Combine(_pathProvider.RKMRoot, "helm-values", $"{chartName}.yaml");
            await File.WriteAllTextAsync(
                valuesPath,
                valuesContent,
                cancellationToken);

            // Install/upgrade via OCI charts.
            var kubeconfigTemporaryPath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(
                    kubeconfigTemporaryPath,
                    kubeconfigData,
                    cancellationToken);
                var arguments = new List<LogicalProcessArgument>()
                {
                    $"--kubeconfig={kubeconfigTemporaryPath}",
                    "--namespace=kube-system",
                    "upgrade",
                    "--install",
                    "--values",
                    valuesPath,
                    chartName,
                    ociUrl,
                };
                if (waitForResourceStabilisation)
                {
                    arguments.Add("--wait");
                }
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = helmPath,
                        Arguments = arguments,
                        WorkingDirectory = Path.GetDirectoryName(helmPath)!,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                return exitCode;
            }
            finally
            {
                File.Delete(kubeconfigTemporaryPath);
            }
        }
    }
}
