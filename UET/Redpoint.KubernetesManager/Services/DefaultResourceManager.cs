namespace Redpoint.KubernetesManager.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class DefaultResourceManager : IResourceManager
    {
        public async Task ExtractResource(string resourceName, string targetPath, Dictionary<string, string> replacements)
        {
            var content = await ReadResource(resourceName, replacements);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using var writer = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            using var streamWriter = new StreamWriter(writer);

            await streamWriter.WriteAsync(content);
        }

        public async Task<string> ReadResource(string resourceName, Dictionary<string, string> replacements)
        {
            resourceName = "Redpoint.KubernetesManager." + resourceName;

            var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            using var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (reader == null)
            {
                throw new InvalidOperationException($"Resource {resourceName} does not exist in the assembly. The following resources are available: {string.Join(", ", names)}");
            }
            using var streamReader = new StreamReader(reader);

            var content = await streamReader.ReadToEndAsync();
            foreach (var kv in replacements)
            {
                content = content.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
            }

            var missingReplacements = Regex.Matches(content, "__[A-Z_]+__");
            var missingReplacementsWeCareAbout = missingReplacements.Where(x =>
                x.Value != "__KUBERNETES_NODE_NAME__" &&
                x.Value != "__CNI_MTU__" &&
                x.Value != "__KUBECONFIG_FILEPATH__");
            if (missingReplacementsWeCareAbout.Any())
            {
                throw new InvalidOperationException($"One or more replacements were not made for {resourceName}: {string.Join(",", missingReplacementsWeCareAbout.Select(x => x.Value))}");
            }

            return content.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }
}
