namespace Redpoint.Uet.Workspace.Descriptors
{
    /// <summary>
    /// Represents a snapshot of the <see cref="SourcePath"/>. When virtualisation is enabled, UEFS
    /// is used to provide an instant copy-of-write view of the folder. When virtualisation is 
    /// disabled, the source path is copied (which is slow).
    /// </summary>
    public record class FolderSnapshotWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string SourcePath { get; set; }

        public required string[] WorkspaceDisambiguators { get; set; }
    }
}
