namespace Redpoint.UET.Workspace.Descriptors
{
    public record class UefsPackageWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string PackageTag { get; set; }

        public required string[] WorkspaceDisambiguators { get; set; }

        /// <summary>
        /// For virtualised snapshots, whether or not the mount should be unmounted after use.
        /// </summary>
        public VirtualisedWorkspaceOptions? WorkspaceOptions { get; set; }
    }
}
