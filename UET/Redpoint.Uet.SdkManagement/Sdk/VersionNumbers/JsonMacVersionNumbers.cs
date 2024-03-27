namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class JsonMacVersionNumbers : IMacVersionNumbers
    {
        public int Priority => 200;

        public bool CanUse(string unrealEnginePath)
        {
            return Path.Exists(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Apple",
                "Apple_SDK.json"));
        }

        public async Task<string> GetXcodeVersion(string unrealEnginePath)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Config",
                "Apple",
                "Apple_SDK.json")).ConfigureAwait(false);
            var dictionary = JsonSerializer.Deserialize(
                json,
                new JsonConfigJsonSerializerContext(new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }).DictionaryStringJsonElement);
            return dictionary!["MainVersion"].ToString();
        }
    }
}
