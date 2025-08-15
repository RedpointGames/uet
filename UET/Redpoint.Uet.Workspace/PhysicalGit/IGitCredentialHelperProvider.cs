namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Redpoint.ProcessExecution;
    using System.Collections.Generic;

    public interface IGitCredentialHelperProvider
    {
        string FilePath { get; }

        IEnumerable<LogicalProcessArgument> Arguments { get; }
    }
}
