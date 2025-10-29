namespace Redpoint.Uet.Workspace.Descriptors
{
    public record class UefsPackageWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string PackageTag { get; set; }

        public required IReadOnlyList<string> WorkspaceDisambiguators { get; set; }

        public required bool NoWriteScratchReuse { get; set; }
    }
}
