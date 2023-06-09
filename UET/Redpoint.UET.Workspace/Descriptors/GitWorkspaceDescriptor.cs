namespace Redpoint.UET.Workspace.Descriptors
{
    public record class GitWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string RepositoryUrl { get; set; }

        public required string RepositoryCommit { get; set; }

        public required string[] AdditionalFolderLayers { get; set; }

        public required string[] WorkspaceDisambiguators { get; set; }

        /// <summary>
        /// If this is for an Unreal Engine project, specifies the project folder name.
        /// </summary>
        public string? ProjectFolderName { get; set; }

        /// <summary>
        /// For virtualised snapshots, whether or not the mount should be unmounted after use.
        /// </summary>
        public VirtualisedWorkspaceOptions? WorkspaceOptions { get; set; }
    }
}
