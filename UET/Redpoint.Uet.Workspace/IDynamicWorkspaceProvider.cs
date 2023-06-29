namespace Redpoint.Uet.Workspace
{
    public interface IDynamicWorkspaceProvider : IWorkspaceProviderBase
    {
        bool UseWorkspaceVirtualisation { get; set; }
    }
}
