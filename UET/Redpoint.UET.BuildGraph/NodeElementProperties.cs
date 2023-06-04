namespace Redpoint.UET.BuildGraph
{
    public record class NodeElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public string Requires { get; set; } = string.Empty;

        public string Produces { get; set; } = string.Empty;
    }
}