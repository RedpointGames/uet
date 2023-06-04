namespace Redpoint.UET.BuildPipeline.Providers.Test.Project.Gauntlet
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Project;
    using Redpoint.UET.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;

    internal class GauntletProjectTestProvider : IProjectTestProvider
    {
        public GauntletProjectTestProvider()
        {
        }

        public string Type => "Gauntlet";

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