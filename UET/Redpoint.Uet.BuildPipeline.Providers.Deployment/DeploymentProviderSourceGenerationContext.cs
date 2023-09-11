namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    internal partial sealed class DeploymentProviderSourceGenerationContext : JsonSerializerContext
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
