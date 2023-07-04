namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam
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

    internal class SteamProjectDeploymentProvider : IProjectDeploymentProvider
    {
        public string Type => "Steam";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => DeploymentProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectDeploymentSteam;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => DeploymentProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, DeploymentProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectDeploymentSteam)!;
        }

        public Task WriteBuildGraphNodesAsync(IBuildGraphEmitContext context, XmlWriter writer, BuildConfigProjectDistribution buildConfigDistribution, IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> elements)
        {
            throw new NotImplementedException();
        }
    }
}
