namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Clang
{
    using System.Text.Json.Serialization;

    internal class ClangCompileCommand
    {
        [JsonPropertyName("directory")]
        public string? Directory { get; set; }

        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("command")]
        public string? Command { get; set; }
    }
}
