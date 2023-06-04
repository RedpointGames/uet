namespace Redpoint.UET.BuildPipeline.Providers.Test
{
    using Redpoint.UET.BuildPipeline.Providers.Test.Plugin.Downstream;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfigPluginTestAutomation))]
    [JsonSerializable(typeof(BuildConfigPluginTestCustom))]
    [JsonSerializable(typeof(BuildConfigPluginTestGauntlet))]
    [JsonSerializable(typeof(BuildConfigPluginTestDownstream))]
    [JsonSerializable(typeof(BuildConfigProjectTestCustom))]
    [JsonSerializable(typeof(BuildConfigProjectTestGauntlet))]
    internal partial class TestProviderSourceGenerationContext : JsonSerializerContext
    {
        public static TestProviderSourceGenerationContext WithStringEnum { get; } = new TestProviderSourceGenerationContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        });
    }
}
