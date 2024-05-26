namespace Redpoint.CppPreprocessor.Parsing
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Precompiled JSON serializer context for <see cref="ParseRequest"/>.
    /// </summary>
    [JsonSerializable(typeof(ParseRequest))]
    public partial class ParseRequestJsonSerializerContext : JsonSerializerContext
    {
    }
}