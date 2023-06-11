namespace Redpoint.UET.Workspace
{
    public interface IDynamicWorkspaceProvider : IWorkspaceProviderBase
    {
        bool UseWorkspaceVirtualisation { get; set; }
    }
}
