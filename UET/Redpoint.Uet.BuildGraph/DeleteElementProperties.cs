namespace Redpoint.Uet.BuildGraph
{
    public record class DeleteElementProperties : ElementProperties
    {
        public required string Files { get; set; }
    }
}