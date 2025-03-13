namespace Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    internal class DefaultDynamicBuildGraphIncludeWriter : IDynamicBuildGraphIncludeWriter
    {
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>[] _pluginPrepare;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>[] _projectPrepare;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>[] _pluginTests;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>[] _projectTests;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>[] _pluginDeployments;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>[] _projectDeployments;
        private readonly IServiceProvider _serviceProvider;

        public DefaultDynamicBuildGraphIncludeWriter(IServiceProvider serviceProvider)
        {
            _pluginPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>>().ToArray();
            _projectPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>>().ToArray();
            _pluginTests = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>>().ToArray();
            _projectTests = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>>().ToArray();
            _pluginDeployments = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>>().ToArray();
            _projectDeployments = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>>().ToArray();
            _serviceProvider = serviceProvider;
        }

        private class BuildGraphEmitContext : IBuildGraphEmitContext
        {
            private readonly ConcurrentDictionary<string, bool> _emitOnce = new ConcurrentDictionary<string, bool>();
            private readonly bool _filterHostToCurrentPlatformOnly;

            public BuildGraphEmitContext(
                IServiceProvider serviceProvider,
                bool filterHostToCurrentPlatformOnly)
            {
                Services = serviceProvider;
                _filterHostToCurrentPlatformOnly = filterHostToCurrentPlatformOnly;
            }

            public IServiceProvider Services { get; }

            public bool CanHostPlatformBeUsed(BuildConfigHostPlatform platform)
            {
                if (!_filterHostToCurrentPlatformOnly)
                {
                    return true;
                }

                if (platform == BuildConfigHostPlatform.Win64 && OperatingSystem.IsWindows())
                {
                    return true;
                }
                else if (platform == BuildConfigHostPlatform.Mac && OperatingSystem.IsMacOS())
                {
                    return true;
                }

                return false;
            }

            public async Task EmitOnceAsync(string name, Func<Task> runOnce)
            {
                if (_emitOnce.TryAdd(name, true))
                {
                    await runOnce().ConfigureAwait(false);
                }
            }
        }

        private static BuildConfigDynamic<TDistribution, TBaseClass>[] FilterDynamicSteps<TDistribution, TBaseClass>(
            BuildConfigDynamic<TDistribution, TBaseClass>[] dynamicSettings,
            string[] filter)
        {
            // Empty filter will not filter for backwards compatibility reasons.
            // This allows UET to be invoked with "--test" / "--deploy" and run all tests & deployments, respectively.
            if (filter.Length == 0) return dynamicSettings;

            return dynamicSettings.Where(x => filter.Contains(x.Name)).ToArray();
        }

        private static async Task WriteBuildGraphNodesAsync<TDistribution, TBaseClass>(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            TDistribution buildConfigDistribution,
            IDynamicProvider<TDistribution, TBaseClass>[] providers,
            BuildConfigDynamic<TDistribution, TBaseClass>[] dynamicSettings,
            BuildConfigDynamic<TDistribution, TBaseClass>[] predefinedDynamicSettings)
        {
            var predefinedDynamicSettingsLookup = predefinedDynamicSettings == null
                ? new Dictionary<string, BuildConfigDynamic<TDistribution, TBaseClass>>()
                : predefinedDynamicSettings.ToDictionary(k => k.Name, v => v);

            var resolvedDynamicSettings = new List<BuildConfigDynamic<TDistribution, TBaseClass>>();
            foreach (var dynamicSetting in dynamicSettings)
            {
                if (dynamicSetting.Type == BuildConfigConstants.Predefined)
                {
                    if (dynamicSetting.DynamicSettings is string predefinedName)
                    {
                        if (predefinedDynamicSettingsLookup.TryGetValue(predefinedName, out var resolvedDynamicSetting))
                        {
                            resolvedDynamicSettings.Add(resolvedDynamicSetting);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Entry '{dynamicSetting.Name}' refers to predefined entry by name '{predefinedName}', but no such predefined entry is available.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Entry '{dynamicSetting.Name}' has invalid 'Predefined' value; it should be a string value.");
                    }
                }
                else
                {
                    resolvedDynamicSettings.Add(dynamicSetting);
                }
            }

            foreach (var byType in resolvedDynamicSettings.GroupBy(x => x.Type))
            {
                var provider = providers.First(x => x.Type == byType.Key);
                await provider.WriteBuildGraphNodesAsync(
                    context,
                    writer,
                    buildConfigDistribution,
                    byType).ConfigureAwait(false);
            }
        }

        public async Task WriteBuildGraphNodeInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfig,
            object buildConfigDistribution,
            string[]? executeTests,
            string[]? executeDeployments)
        {
            var emitContext = new BuildGraphEmitContext(
                _serviceProvider,
                filterHostToCurrentPlatformOnly);

            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "  ",
                Async = true,
            }))
            {
                await writer.WriteStartDocumentAsync().ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "BuildGraph", "http://www.epicgames.com/BuildGraph").ConfigureAwait(false);

                if (buildConfigDistribution is BuildConfigPluginDistribution pluginDistribution)
                {
                    if (pluginDistribution.Tests != null && executeTests != null)
                    {
                        var filteredTests = FilterDynamicSteps(pluginDistribution.Tests, executeTests);

                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginTests,
                            filteredTests,
                            buildConfig is BuildConfigPlugin buildConfigPlugin && buildConfigPlugin.Tests != null
                                ? buildConfigPlugin.Tests
                                : []).ConfigureAwait(false);
                    }

                    if (pluginDistribution.Deployment != null && executeDeployments != null)
                    {
                        var filteredDeployments = FilterDynamicSteps(pluginDistribution.Deployment, executeDeployments);

                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginDeployments,
                            filteredDeployments,
                            []).ConfigureAwait(false);
                    }
                }
                else if (buildConfigDistribution is BuildConfigProjectDistribution projectDistribution)
                {
                    if (projectDistribution.Tests != null && executeTests != null)
                    {
                        var filteredTests = FilterDynamicSteps(projectDistribution.Tests, executeTests);

                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectTests,
                            filteredTests,
                            []).ConfigureAwait(false);
                    }

                    if (projectDistribution.Deployment != null && executeDeployments != null)
                    {
                        var filteredDeployments = FilterDynamicSteps(projectDistribution.Deployment, executeDeployments);

                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectDeployments,
                            filteredDeployments,
                            []).ConfigureAwait(false);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
                await writer.WriteEndDocumentAsync().ConfigureAwait(false);
            }
        }

        public async Task WriteBuildGraphMacroInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfig,
            object buildConfigDistribution)
        {
            var emitContext = new BuildGraphEmitContext(
                _serviceProvider,
                filterHostToCurrentPlatformOnly);

            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "  ",
                Async = true,
            }))
            {
                await writer.WriteStartDocumentAsync().ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "BuildGraph", "http://www.epicgames.com/BuildGraph").ConfigureAwait(false);

                if (buildConfigDistribution is BuildConfigPluginDistribution pluginDistribution)
                {
                    if (pluginDistribution.Prepare != null)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginPrepare,
                            pluginDistribution.Prepare,
                            []).ConfigureAwait(false);
                    }
                }
                else if (buildConfigDistribution is BuildConfigProjectDistribution projectDistribution)
                {
                    if (projectDistribution.Prepare != null)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectPrepare,
                            projectDistribution.Prepare,
                            []).ConfigureAwait(false);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
                await writer.WriteEndDocumentAsync().ConfigureAwait(false);
            }
        }
    }
}
