namespace Redpoint.UET.BuildGraph
{
    public record class PropertyElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required string Value { get; set; }
    }
}