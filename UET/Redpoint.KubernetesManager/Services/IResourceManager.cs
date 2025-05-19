namespace Redpoint.KubernetesManager.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IResourceManager
    {
        Task ExtractResource(string resourceName, string targetPath, Dictionary<string, string> replacements);

        Task<string> ReadResource(string resourceName, Dictionary<string, string> replacements);
    }
}
