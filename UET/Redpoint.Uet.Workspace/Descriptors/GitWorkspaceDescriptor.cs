namespace Redpoint.Uet.Workspace.Descriptors
{
    using System.Diagnostics.CodeAnalysis;

    public record class GitWorkspaceDescriptor : IWorkspaceDescriptor
    {
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Git URLs are not compatible with the Uri object.")]
        public required string RepositoryUrl { get; set; }

        public required string RepositoryCommitOrRef { get; set; }

        public required IReadOnlyList<string> AdditionalFolderLayers { get; set; }

        public required IReadOnlyList<string> AdditionalFolderZips { get; set; }

        public required IReadOnlyList<string> WorkspaceDisambiguators { get; set; }

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
