namespace Redpoint.UET.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;

    internal class BackblazeB2PluginDeploymentProvider : IPluginDeploymentProvider
    {
        public string Type => "BackblazeB2";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, DeploymentProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginDeploymentBackblazeB2)!;
        }

        public Task WriteBuildGraphNodesAsync(IBuildGraphEmitContext context, XmlWriter writer, BuildConfigPluginDistribution buildConfigDistribution, IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IDeploymentProvider>> elements)
        {
            throw new NotImplementedException();
        }
    }
}
