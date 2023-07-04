namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Gauntlet
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class GauntletPluginTestProvider : IPluginTestProvider
    {
        public GauntletPluginTestProvider()
        {
        }

        public string Type => "Gauntlet";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestGauntlet;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => TestProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestGauntlet)!;
        }

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> elements)
        {
            throw new NotImplementedException();
        }
    }
}