namespace Redpoint.Uet.BuildGraph
{
    public record class DynamicNodeAppendElementProperties : ElementProperties
    {
        public required string NodeName { get; set; }
        public bool MustPassForLaterDeployment { get; set; }
    }
}