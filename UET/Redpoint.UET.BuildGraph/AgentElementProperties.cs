namespace Redpoint.UET.BuildGraph
{
    public record class AgentElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required string Type { get; set; }
    }
}