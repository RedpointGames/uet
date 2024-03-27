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
            return new WindowsSdkInstallerTarget
            {
                WindowsSdkPreferredVersion = VersionNumber.Parse(dictionary!["MainVersion"].ToString()),
                VisualCppMinimumVersion = VersionNumber.Parse(dictionary!["MinimumVisualCppVersion"].ToString()),
                SuggestedComponents = Array.Empty<string>(),
            };
        }
    }
}
