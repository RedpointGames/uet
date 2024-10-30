namespace Redpoint.Uet.Configuration
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfig))]
    [JsonSerializable(typeof(BuildConfigEngine))]
    [JsonSerializable(typeof(BuildConfigPlugin))]
    [JsonSerializable(typeof(BuildConfigProject))]
    [JsonSerializable(typeof(BuildConfigEngineIncludeFragment))]
    [JsonSerializable(typeof(BuildConfigPluginIncludeFragment))]
    [JsonSerializable(typeof(BuildConfigProjectIncludeFragment))]
    public partial class BuildConfigSourceGenerationContext : JsonSerializerContext
    {
        public static BuildConfigSourceGenerationContext Create(
            IServiceProvider serviceProvider,
            string basePathForIncludes)
        {
            return new BuildConfigSourceGenerationContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new BuildConfigConverter(basePathForIncludes),
                    new JsonStringEnumConverter(),
                    new BuildConfigPrepareConverter<BuildConfigPluginDistribution>(serviceProvider),
                    new BuildConfigPrepareConverter<BuildConfigProjectDistribution>(serviceProvider),
                    new BuildConfigTestConverter<BuildConfigPluginDistribution>(serviceProvider),
                    new BuildConfigTestConverter<BuildConfigProjectDistribution>(serviceProvider),
                    new BuildConfigDeploymentConverter<BuildConfigPluginDistribution>(serviceProvider),
                    new BuildConfigDeploymentConverter<BuildConfigProjectDistribution>(serviceProvider),
                    new BuildConfigTargetPlatformConverter(),
                }
            });
        }
    }
}
