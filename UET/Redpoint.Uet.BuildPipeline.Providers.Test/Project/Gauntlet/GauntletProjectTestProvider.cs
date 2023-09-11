namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class GauntletProjectTestProvider : IProjectTestProvider
    {
        public GauntletProjectTestProvider()
        {
        }

        public string Type => "Gauntlet";

        public IRuntimeJson DynamicSettings { get; } = new TestProviderRuntimeJson(TestProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectTestGauntlet;

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