namespace Redpoint.Uet.BuildGraph
{
    public record class ZipElementProperties : ElementProperties
    {
        public required string FromDir { get; set; }

        public required string Files { get; set; }

        public required string ZipFile { get; set; }

        public required string Tag { get; set; }
    }
}