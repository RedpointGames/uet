namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Gauntlet
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class GauntletPluginTestProvider : IPluginTestProvider
    {
        public GauntletPluginTestProvider()
        {
        }

        public string Type => "Gauntlet";

        public IRuntimeJson DynamicSettings { get; } = new TestProviderRuntimeJson(TestProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginTestGauntlet;

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