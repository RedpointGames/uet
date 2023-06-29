namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Downstream
{
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;

    internal class DownstreamPluginTestProvider : IPluginTestProvider
    {
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public DownstreamPluginTestProvider(IGlobalArgsProvider? globalArgsProvider = null)
        {
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "Downstream";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestDownstream)!;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> entries)
        {
            var castedEntries = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestCustom)x.DynamicSettings))
                .ToList();

            await writer.WriteAgentAsync(
                new AgentElementProperties
                {
                    Name = $"Downstream Tests",
                    Type = "Meta"
                },
                async writer =>
                {
                    foreach (var entry in castedEntries)
                    {
                        var nodeName = $"Downstream {entry.name}";

                        await writer.WriteNodeAsync(
                            new NodeElementProperties
                            {
                                Name = nodeName,
                                Requires = "#PackagedPlugin"
                            },
                            async writer =>
                            {
                                await writer.WriteSpawnAsync(
                                    new SpawnElementProperties
                                    {
                                        Exe = "$(UETPath)",
                                        Arguments = (_globalArgsProvider?.GlobalArgsArray ?? new string[0]).Concat(new[]
                                        {
                                            "internal",
                                            "run-downstream-test",
                                            "--downstream-test",
                                            $@"""{entry.name}""",
                                            "--engine-path",
                                            $@"""$(EnginePath)""",
                                            "--distribution",
                                            $@"""$(Distribution)""",
                                            "--packaged-plugin-path",
                                            $@"""$(TempPath)/$(PackageFolder)/""",
                                        }).ToArray()
                                    });
                            });
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = nodeName,
                                MustPassForLaterDeployment = true,
                            });
                    }
                });
        }
    }
}