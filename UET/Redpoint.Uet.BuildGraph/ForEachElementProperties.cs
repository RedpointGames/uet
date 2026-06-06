namespace Redpoint.Uet.BuildGraph
{
    public record class ForEachElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required IReadOnlyCollection<string> Values { get; set; }
    }
}