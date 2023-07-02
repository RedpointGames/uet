namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;

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