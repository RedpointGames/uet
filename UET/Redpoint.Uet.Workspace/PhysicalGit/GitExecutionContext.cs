namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using System;
    using System.Collections.Generic;

    internal class GitExecutionContext
    {
        public required string Git { get; init; }
        public required IReadOnlyDictionary<string, string> GitEnvs { get; init; }
        public required Func<GitTemporaryEnvVarsForFetch> FetchEnvironmentVariablesFactory { get; init; }
        public required bool EnableSubmoduleSupport { get; set; }
        public required bool AllowSubmoduleSupport { get; set; }
        public required bool AssumeLfs { get; set; }
    }
}
