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
        private readonly IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>[] _pluginTests;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>[] _projectTests;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>[] _pluginDeployments;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>[] _projectDeployments;
        private readonly IServiceProvider _serviceProvider;

        public DefaultDynamicBuildGraphIncludeWriter(IServiceProvider serviceProvider)
        {
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
                    await runOnce();
                }
            }
        }

        private async Task WriteBuildGraphNodesAsync<TDistribution, TBaseClass>(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            TDistribution buildConfigDistribution,
            IDynamicProvider<TDistribution, TBaseClass>[] providers,
            BuildConfigDynamic<TDistribution, TBaseClass>[] dynamicSettings)
        {
            foreach (var byType in dynamicSettings.GroupBy(x => x.Type))
            {
                var provider = providers.First(x => x.Type == byType.Key);
                await provider.WriteBuildGraphNodesAsync(
                    context,
                    writer,
                    buildConfigDistribution,
                    byType);
            }
        }

        public async Task WriteBuildGraphInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfigDistribution,
            bool executeTests,
            bool executeDeployment)
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
                await writer.WriteStartDocumentAsync();
                await writer.WriteStartElementAsync(null, "BuildGraph", "http://www.epicgames.com/BuildGraph");

                if (buildConfigDistribution is BuildConfigPluginDistribution pluginDistribution)
                {
                    if (pluginDistribution.Tests != null && executeTests)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginTests,
                            pluginDistribution.Tests);
                    }

                    if (pluginDistribution.Deployment != null && executeDeployment)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginDeployments,
                            pluginDistribution.Deployment);
                    }
                }
                else if (buildConfigDistribution is BuildConfigProjectDistribution projectDistribution)
                {
                    if (projectDistribution.Tests != null && executeTests)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectTests,
                            projectDistribution.Tests);
                    }

                    if (projectDistribution.Deployments != null && executeDeployment)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectDeployments,
                            projectDistribution.Deployments);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }

                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
            }
        }
    }
}
