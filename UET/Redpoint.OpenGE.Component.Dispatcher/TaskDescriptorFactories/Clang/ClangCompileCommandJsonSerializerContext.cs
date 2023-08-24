namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Clang
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ClangCompileCommand[]))]
    internal partial class ClangCompileCommandJsonSerializerContext : JsonSerializerContext
    {
    }
}
