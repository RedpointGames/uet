namespace Io.Json.Api
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class TestJsonWithValidatedContext
    {
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API.")]
        public TestJson[] Tests { get; set; } = [];

        public long BuildId { get; set; }

        public long PipelineId { get; set; }

        public long ProjectId { get; set; }

        public long NamespaceId { get; set; }
    }
}
