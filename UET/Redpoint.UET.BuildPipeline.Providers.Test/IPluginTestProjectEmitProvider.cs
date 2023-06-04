namespace Redpoint.UET.BuildPipeline.Providers.Test
{
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;

    internal interface IPluginTestProjectEmitProvider
    {
        string GetTestProjectUProjectFilePath(BuildConfigHostPlatform platform);

        string GetTestProjectDirectoryPath(BuildConfigHostPlatform platform);

        string GetTestProjectTags(BuildConfigHostPlatform platform);

        Task EnsureTestProjectNodesArePresentAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer);
    }
}