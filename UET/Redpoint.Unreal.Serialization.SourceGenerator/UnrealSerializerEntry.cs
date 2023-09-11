namespace Redpoint.Unreal.Serialization.SourceCodeGenerator
{
    internal sealed class UnrealSerializerEntry
    {
        public string? Namespace { get; set; }
        public string? Class { get; set; }
        public List<string> SerializableClassNames { get; } = new List<string>();
        public List<string> TopLevelAssetPathClassNames { get; } = new List<string>();
    }
}