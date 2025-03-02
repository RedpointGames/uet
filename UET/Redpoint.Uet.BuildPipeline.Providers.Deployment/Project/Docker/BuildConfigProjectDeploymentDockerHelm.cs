namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentDockerHelm
    {
        /// <summary>
        /// The name of the Helm chart that will be installed or upgraded.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The namespace to install the Helm chart into.
        /// </summary>
        [JsonPropertyName("Namespace"), JsonRequired]
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// The Kubernetes context to deploy to.
        /// </summary>
        [JsonPropertyName("KubeContext"), JsonRequired]
        public string KubeContext { get; set; } = string.Empty;

        /// <summary>
        /// The path to the Helm chart to deploy. The Helm chart will be passed --set agones.image "image:tag" --set agones.version "version"
        /// </summary>
        [JsonPropertyName("HelmChartPath"), JsonRequired]
        public string HelmChartPath { get; set; } = string.Empty;

        /// <summary>
        /// Additional --set key=value pairs to pass to this specific Helm deployment.
        /// </summary>
        [JsonPropertyName("HelmValues")]
        public Dictionary<string, string>? HelmValues { get; set; }

    }
}
