namespace Redpoint.Uet.BuildGraph
{
    public record class StringOpElementProperties : ElementProperties
    {
        public required string Input { get; set; }

        public required string Method { get; set; }

        public required string Output { get; set; }

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }
}