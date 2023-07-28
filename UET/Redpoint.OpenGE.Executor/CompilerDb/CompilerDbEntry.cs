namespace Redpoint.OpenGE.Executor.CompilerDb
{
    using Redpoint.Concurrency;
    using System.Collections.Generic;
    using System.IO;

    internal record class CompilerDbEntry
    {
        public required string Path { get; set; }

        public Gate Ready { get; set; } = new Gate();

        public bool WasCancelled { get; set; } = false;

        public string[] ImmediateDependsOn { get; set; } = Array.Empty<string>();
    }
}
