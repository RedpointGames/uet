namespace Redpoint.CloudFramework.CLI
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(LanguageDictionary))]
    internal partial class LanguageJsonSerializerContext : JsonSerializerContext
    {
    }
}
