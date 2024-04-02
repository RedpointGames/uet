namespace Redpoint.Uet.BuildGraph
{
    public record class CommandElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }
}