namespace Redpoint.Uet.Workspace.Descriptors
{
    /// <summary>
    /// Represents that the <see cref="AliasedPath"/> will be returned as the workspace path.
    /// </summary>
    public record class FolderAliasWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string AliasedPath { get; set; }
    }
}
