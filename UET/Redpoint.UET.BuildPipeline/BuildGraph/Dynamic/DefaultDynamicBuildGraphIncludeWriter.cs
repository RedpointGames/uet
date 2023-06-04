namespace Redpoint.UET.BuildPipeline.BuildGraph.Dynamic
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.Configuration;
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    internal class DefaultDynamicBuildGraphIncludeWriter : IDynamicBuildGraphIncludeWriter
    {
        private readonly IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>[] _pluginTests;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>[] _projectTests;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>[] _pluginDeployments;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>[] _projectDeployments;

        public DefaultDynamicBuildGraphIncludeWriter(IServiceProvider serviceProvider)
        {
            _pluginTests = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>>().ToArray();
            _projectTests = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>>().ToArray();
            _pluginDeployments = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>>().ToArray();
            _projectDeployments = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>>().ToArray();
        }

        private class BuildGraphEmitContext : IBuildGraphEmitContext
        {
            private readonly ConcurrentDictionary<string, bool> _emitOnce = new ConcurrentDictionary<string, bool>();
            private readonly bool _filterHostToCurrentPlatformOnly;

            public BuildGraphEmitContext(bool filterHostToCurrentPlatformOnly)
            {
                _filterHostToCurrentPlatformOnly = filterHostToCurrentPlatformOnly;
            }

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
            object buildConfigDistribution)
        {
            var emitContext = new BuildGraphEmitContext(filterHostToCurrentPlatformOnly);

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
                    if (pluginDistribution.Tests != null)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            pluginDistribution,
                            _pluginTests,
                            pluginDistribution.Tests);
                    }

                    if (pluginDistribution.Deployment != null)
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
                    if (projectDistribution.Tests != null)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectTests,
                            projectDistribution.Tests);
                    }

                    if (projectDistribution.Deployment != null)
                    {
                        await WriteBuildGraphNodesAsync(
                            emitContext,
                            writer,
                            projectDistribution,
                            _projectDeployments,
                            projectDistribution.Deployment);
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
