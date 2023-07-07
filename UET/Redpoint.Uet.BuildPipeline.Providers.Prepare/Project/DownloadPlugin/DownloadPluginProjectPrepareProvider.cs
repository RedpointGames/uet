namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    internal class DownloadPluginProjectPrepareProvider : IProjectPrepareProvider
    {
        public string Type => "DownloadPlugin";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareDownloadPlugin;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => PrepareProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareDownloadPlugin)!;
        }

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context, 
            XmlWriter writer, 
            BuildConfigProjectDistribution buildConfigDistribution, 
IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareCustom)x.DynamicSettings))
                .ToList();

            throw new NotImplementedException();
        }

        public Task RunBeforeBuildGraphAsync(BuildConfigProjectDistribution buildConfigDistribution, IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries, string repositoryRoot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
