namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Meta;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentMeta))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentDocker))]
    internal sealed partial class DeploymentProviderSourceGenerationContext : JsonSerializerContext
    {
        public static DeploymentProviderSourceGenerationContext WithStringEnum { get; } = new DeploymentProviderSourceGenerationContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        });
    }
}
