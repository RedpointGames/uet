namespace Redpoint.UET.BuildPipeline.Providers.Deployment
{
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    internal partial class DeploymentProviderSourceGenerationContext : JsonSerializerContext
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
