namespace Redpoint.Uet.BuildGraph
{
    public record class SanitizeReceiptElementProperties : ElementProperties
    {
        public required string Files { get; set; }
    }
}