namespace Redpoint.OpenGE.JobXml
{
    using System.Collections.Generic;

    public record class Job
    {
        public required Dictionary<string, JobEnvironment> Environments { get; init; }

        public required Dictionary<string, JobProject> Projects { get; init; }
    }
}
