namespace Redpoint.Uet.BuildGraph
{
    public record class TagElementProperties : ElementProperties
    {
        public string? BaseDir { get; set; }

        public required string Files { get; set; }

        public required string With { get; set; }

        public string? FileLists { get; set; }

        public string? Filter { get; set; }

        public string? Except { get; set; }
    }
}