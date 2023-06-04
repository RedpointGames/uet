namespace Redpoint.UET.BuildPipeline.Providers.Deployment.Project.Custom
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomProjectDeploymentProvider : IProjectDeploymentProvider
    {
        public string Type => "Custom";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, DeploymentProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectDeploymentCustom)!;
        }

        public Task WriteBuildGraphNodesAsync(IBuildGraphEmitContext context, XmlWriter writer, BuildConfigProjectDistribution buildConfigDistribution, IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> elements)
        {
            throw new NotImplementedException();
        }
    }
}
