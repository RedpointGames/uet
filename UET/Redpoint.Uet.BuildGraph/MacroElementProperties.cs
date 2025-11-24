namespace Redpoint.Uet.BuildGraph
{
    public record class MacroElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }
}