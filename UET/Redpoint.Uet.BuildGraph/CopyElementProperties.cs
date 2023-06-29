namespace Redpoint.Uet.BuildGraph
{
    public record class CopyElementProperties : ElementProperties
    {
        public required string Files { get; set; }

        public required string From { get; set; }

        public required string To { get; set; }

        public string? Tag { get; set; }
    }
}