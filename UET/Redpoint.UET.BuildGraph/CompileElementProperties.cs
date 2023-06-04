namespace Redpoint.UET.BuildGraph
{
    public record class CompileElementProperties : ElementProperties
    {
        public required string Target { get; set; }

        public required string Platform { get; set; }

        public required string Configuration { get; set; }

        public required string Tag { get; set; }

        public required string[] Arguments { get; set; }
    }
}