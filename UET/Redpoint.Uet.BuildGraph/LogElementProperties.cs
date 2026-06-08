namespace Redpoint.Uet.BuildGraph
{
    public record class LogElementProperties : ElementProperties
    {
        public string? Message { get; set; }

        public string? Files { get; set; }

        public bool IncludeContents { get; set; } = false;
    }

    public record class TraceElementProperties : ElementProperties
    {
        public string? Message { get; set; }

        public bool ReportOnExecution { get; set; } = false;
    }
}