namespace Redpoint.UET.BuildPipeline.Providers.Test.Project.Custom
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Project;
    using Redpoint.UET.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.UET.Configuration.Plugin;

    internal class CustomProjectTestProvider : IProjectTestProvider
    {
        public CustomProjectTestProvider()
        {
        }

        public string Type => "Custom";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectTestCustom)!;
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