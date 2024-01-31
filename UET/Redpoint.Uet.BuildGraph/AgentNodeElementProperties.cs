namespace Redpoint.Uet.BuildGraph
{
    public record class AgentNodeElementProperties : ElementProperties
    {
        public required string AgentStage { get; set; }

        public required string AgentType { get; set; }

        public required string NodeName { get; set; }

        public string Requires { get; set; } = string.Empty;

        public string Produces { get; set; } = string.Empty;
    }
}