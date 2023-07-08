namespace Redpoint.RuntimeJson.SourceGenerator
{
    internal class RuntimeJsonSerializerEntry
    {
        public string? Namespace { get; set; }
        public string? Class { get; set; }
        public string? JsonSerializerContextType { get; set; }
        public List<string> SerializableClassNames { get; } = new List<string>();
    }
}