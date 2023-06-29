namespace Redpoint.Uet.BuildGraph
{
    public record class ExpandElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    }
}