namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfigPluginPrepareCustom))]
    [JsonSerializable(typeof(BuildConfigProjectPrepareCustom))]
    internal partial class PrepareProviderSourceGenerationContext : JsonSerializerContext
    {
        public static PrepareProviderSourceGenerationContext WithStringEnum { get; } = new PrepareProviderSourceGenerationContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        });
    }
}