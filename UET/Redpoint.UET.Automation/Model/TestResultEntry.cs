namespace Redpoint.UET.Automation.Model
{
    using System;

    public record class TestResultEntry
    {
        public required TestResultEntryCategory Category { get; set; }

        public required string Message { get; set; }

        public required string Filename { get; set; }

        public required int LineNumber { get; set; }
    }
}
