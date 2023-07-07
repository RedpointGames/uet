namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomProjectPrepareProvider : IProjectPrepareProvider
    {
        private readonly IScriptExecutor _scriptExecutor;

        public CustomProjectPrepareProvider(
            IScriptExecutor scriptExecutor)
        {
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareCustom;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => PrepareProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(
            ref Utf8JsonReader reader, 
            JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareCustom)!;
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

        public async Task RunBeforeBuildGraphAsync(
            BuildConfigProjectDistribution buildConfigDistribution, 
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigProjectPrepareRunBefore>()).Contains(BuildConfigProjectPrepareRunBefore.BuildGraph)))
            {
                await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = entry.settings.ScriptPath,
                        Arguments = Array.Empty<string>(),
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
            }
        }
    }
}
