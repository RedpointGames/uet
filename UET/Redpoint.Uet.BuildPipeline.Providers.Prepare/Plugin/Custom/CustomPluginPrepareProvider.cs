namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomPluginPrepareProvider : IPluginPrepareProvider
    {
        private readonly IScriptExecutor _scriptExecutor;

        public CustomPluginPrepareProvider(
            IScriptExecutor scriptExecutor)
        {
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginPrepareCustom;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => PrepareProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(
            ref Utf8JsonReader reader, 
            JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginPrepareCustom)!;
        }

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context, 
            XmlWriter writer, 
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            throw new NotImplementedException();
        }

        public async Task RunBeforeBuildGraphAsync(
            BuildConfigPluginDistribution buildConfigDistribution, 
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries,
            CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigPluginPrepareRunBefore>()).Contains(BuildConfigPluginPrepareRunBefore.BuildGraph)))
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
