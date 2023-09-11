namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(DeploymentProviderSourceGenerationContext))]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    internal partial sealed class DeploymentProviderRuntimeJson
    {
    }
}
