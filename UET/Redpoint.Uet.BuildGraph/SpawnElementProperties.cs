namespace Redpoint.Uet.BuildGraph
{
    public record class SpawnElementProperties : ElementProperties
    {
        public required string Exe { get; set; }

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }
}