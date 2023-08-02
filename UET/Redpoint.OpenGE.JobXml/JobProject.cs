namespace Redpoint.OpenGE.JobXml
{
    using System.Collections.Generic;

    public record class JobProject
    {
        public required string Name { get; init; }

        public required string Env { get; init; }

        public required Dictionary<string, JobTask> Tasks { get; init; }
    }
}
