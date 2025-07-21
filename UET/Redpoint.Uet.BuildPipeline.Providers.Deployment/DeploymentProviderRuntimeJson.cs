namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Meta;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(DeploymentProviderSourceGenerationContext))]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentMeta))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentGooglePlay))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentDocker))]
    internal sealed partial class DeploymentProviderRuntimeJson
    {
    }
}
