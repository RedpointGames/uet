namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class JsonAndroidVersionNumbers : IAndroidVersionNumbers
    {
        public int Priority => 200;

        public bool CanUse(string unrealEnginePath)
        {
            return Path.Exists(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Android",
                "Android_SDK.json"));
        }

        public async Task<(string platforms, string buildTools, string cmake, string ndk)> GetVersions(string unrealEnginePath)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Android",
                "Android_SDK.json")).ConfigureAwait(false);
            var dictionary = JsonSerializer.Deserialize(
                json,
                new JsonConfigJsonSerializerContext(new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }).DictionaryStringJsonElement);
            return (
                dictionary!["platforms"].ToString(),
                dictionary!["build-tools"].ToString(),
                dictionary!["cmake"].ToString(),
                dictionary!["ndk"].ToString());
        }
    }
}
