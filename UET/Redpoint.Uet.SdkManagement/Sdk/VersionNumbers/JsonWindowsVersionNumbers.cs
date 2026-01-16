namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class JsonWindowsVersionNumbers : IWindowsVersionNumbers
    {
        public int Priority => 200;

        public bool CanUse(string unrealEnginePath)
        {
            return Path.Exists(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Windows",
                "Windows_SDK.json"));
        }

        public async Task<WindowsSdkInstallerTarget> GetWindowsVersionNumbersAsync(
            string unrealEnginePath)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Windows",
                "Windows_SDK.json")).ConfigureAwait(false);
            var dictionary = JsonSerializer.Deserialize(
                json,
                new JsonConfigJsonSerializerContext(new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }).DictionaryStringJsonElement);

            VersionNumber visualCppMinimumVersion = VersionNumber.Parse(dictionary!["MinimumVisualCppVersion"].ToString());
            List<VersionRange> preferredVisualCppVersions = new();
            List<VersionRange> bannedVisualCppVersions = new();

            if (dictionary.TryGetValue("PreferredVisualCppVersions", out var preferredJsonElement) &&
                preferredJsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var preferredJsonSubElement in preferredJsonElement.EnumerateArray())
                {
                    if (preferredJsonSubElement.ValueKind == JsonValueKind.String)
                    {
                        preferredVisualCppVersions.Add(VersionRange.Parse(preferredJsonSubElement.GetString()!));
                    }
                }
            }

            if (dictionary.TryGetValue("BannedVisualCppVersions", out var bannedJsonElement) &&
                bannedJsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var bannedJsonSubElement in bannedJsonElement.EnumerateArray())
                {
                    if (bannedJsonSubElement.ValueKind == JsonValueKind.String)
                    {
                        bannedVisualCppVersions.Add(VersionRange.Parse(bannedJsonSubElement.GetString()!));
                    }
                }
            }

            return new WindowsSdkInstallerTarget
            {
                WindowsSdkPreferredVersion = VersionNumber.Parse(dictionary!["MainVersion"].ToString()),
                MinimumVisualCppVersion = visualCppMinimumVersion,
                PreferredVisualCppVersions = preferredVisualCppVersions,
                BannedVisualCppVersions = bannedVisualCppVersions,
                SuggestedComponents = Array.Empty<string>(),
            };
        }
    }
}
