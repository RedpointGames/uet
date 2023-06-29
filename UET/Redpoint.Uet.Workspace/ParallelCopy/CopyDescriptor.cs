namespace Redpoint.Uet.Workspace.ParallelCopy
{
    using System.Collections.Generic;

    internal class CopyDescriptor
    {
        public required string SourcePath { get; set; }

        public required string DestinationPath { get; set; }

        public required IReadOnlySet<string> DirectoriesToRemoveExtraFilesUnder { get; set; }

        public required IReadOnlySet<string> ExcludePaths { get; set; }
    }
}
