namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class GauntletProjectTestProvider : IProjectTestProvider
    {
        public GauntletProjectTestProvider()
        {
        }

        public string Type => "Gauntlet";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => TestProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectTestGauntlet;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => TestProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectTestGauntlet)!;
        }

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, ITestProvider>> dynamicSettings)
        {
            throw new NotImplementedException();
        }
    }
}