namespace Redpoint.OpenGE.Executor.BuildSetData
{
    using System.Collections.Generic;

    internal record class BuildSetEnvironment
    {
        public required string Name { get; init; }

        public required Dictionary<string, BuildSetTool> Tools { get; init; }

        public required Dictionary<string, string> Variables { get; init; }
    }
}
