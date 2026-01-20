namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(EngineBuildVersion))]
    internal partial class EngineBuildVersionJsonSerializerContext : JsonSerializerContext
    {
    }
}
