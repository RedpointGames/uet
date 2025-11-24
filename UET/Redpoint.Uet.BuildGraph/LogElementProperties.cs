namespace Redpoint.Uet.BuildGraph
{
    public record class LogElementProperties : ElementProperties
    {
        public string? Message { get; set; }

        public string? Files { get; set; }

        public bool IncludeContents { get; set; } = false;
    }
}