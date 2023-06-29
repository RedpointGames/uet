namespace Redpoint.Uet.Workspace.Descriptors
{
    public record class UefsPackageWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string PackageTag { get; set; }

        public required string[] WorkspaceDisambiguators { get; set; }
    }
}
