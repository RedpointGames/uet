namespace Redpoint.OpenGE.Executor.BuildSetData
{
    using System.Collections.Generic;

    internal record class BuildSet
    {
        public required Dictionary<string, BuildSetEnvironment> Environments { get; init; }

        public required Dictionary<string, BuildSetProject> Projects { get; init; }
    }
}
