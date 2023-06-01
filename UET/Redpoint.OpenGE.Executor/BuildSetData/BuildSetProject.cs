namespace Redpoint.OpenGE.Executor.BuildSetData
{
    using System.Collections.Generic;

    internal record class BuildSetProject
    {
        public required string Name { get; init; }

        public required string Env { get; init; }

        public required Dictionary<string, BuildSetTask> Tasks { get; init; }
    }
}
