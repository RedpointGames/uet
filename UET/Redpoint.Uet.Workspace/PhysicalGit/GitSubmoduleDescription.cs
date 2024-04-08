namespace Redpoint.Uet.Workspace.PhysicalGit
{
    internal class GitSubmoduleDescription
    {
        public required string Id { get; set; }
        public required string Path { get; set; }
        public required string Url { get; set; }
        public required bool ExcludeOnMac { get; set; }
    }
}
