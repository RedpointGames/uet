namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildJobJson))]
    public partial class BuildJobJsonSourceGenerationContext : JsonSerializerContext
    {
        public static BuildJobJsonSourceGenerationContext Create(IServiceProvider serviceProvider)
        {
            return new BuildJobJsonSourceGenerationContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new BuildConfigPrepareConverter<BuildConfigPluginDistribution>(serviceProvider),
                    new BuildConfigPrepareConverter<BuildConfigProjectDistribution>(serviceProvider),
                }
            });
        }
    }
}