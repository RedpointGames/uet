namespace Redpoint.UET.Configuration
{
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildConfig))]
    [JsonSerializable(typeof(BuildConfigEngine))]
    [JsonSerializable(typeof(BuildConfigPlugin))]
    [JsonSerializable(typeof(BuildConfigProject))]
    public partial class BuildConfigSourceGenerationContext : JsonSerializerContext
    {
    }
}
