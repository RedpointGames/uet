namespace Redpoint.Uet.Workspace.Descriptors
{
    public record class TemporaryWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string Name { get; set; }
    }
}
