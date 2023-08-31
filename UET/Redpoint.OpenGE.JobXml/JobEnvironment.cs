namespace Redpoint.OpenGE.JobXml
{
    using System.Collections.Generic;

    public record class JobEnvironment
    {
        public required string Name { get; init; }

        public required Dictionary<string, JobTool> Tools { get; init; }

        public required Dictionary<string, string> Variables { get; init; }
    }
}
