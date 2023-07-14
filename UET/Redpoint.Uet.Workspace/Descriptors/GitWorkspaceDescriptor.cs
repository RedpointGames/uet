namespace Redpoint.Uet.Workspace.Descriptors
{
    public record class GitWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string RepositoryUrl { get; set; }

        public required string RepositoryCommitOrRef { get; set; }

        public required string[] AdditionalFolderLayers { get; set; }

        public required string[] AdditionalFolderZips { get; set; }

        public required string[] WorkspaceDisambiguators { get; set; }

        public required string? WindowsSharedGitCachePath { get; set; }

        public required string? MacSharedGitCachePath { get; set; }

        /// <summary>
        /// If this is for an Unreal Engine project, specifies the project folder name.
        /// </summary>
        public string? ProjectFolderName { get; set; }

        /// <summary>
        /// If set, this is a checkout of the engine itself, instead of a project or plugin.
        /// </summary>
        public bool IsEngineBuild { get; set; }
    }
}
