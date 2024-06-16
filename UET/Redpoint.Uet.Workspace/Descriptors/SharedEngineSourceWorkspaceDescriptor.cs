namespace Redpoint.Uet.Workspace.Descriptors
{
    public record class SharedEngineSourceWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string NetworkShare { get; set; }
    }
}
