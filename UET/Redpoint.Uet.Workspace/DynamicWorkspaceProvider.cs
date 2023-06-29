namespace Redpoint.Uet.Workspace
{
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DynamicWorkspaceProvider : IDynamicWorkspaceProvider
    {
        private readonly IPhysicalWorkspaceProvider _physicalWorkspaceProvider;
        private readonly IVirtualWorkspaceProvider _virtualWorkspaceProvider;

        public DynamicWorkspaceProvider(
            IPhysicalWorkspaceProvider physicalWorkspaceProvider,
            IVirtualWorkspaceProvider virtualWorkspaceProvider)
        {
            _physicalWorkspaceProvider = physicalWorkspaceProvider;
            _virtualWorkspaceProvider = virtualWorkspaceProvider;
        }

        public bool UseWorkspaceVirtualisation { get; set; }

        public bool ProvidesFastCopyOnWrite
        {
            get
            {
                if (UseWorkspaceVirtualisation)
                {
                    return _virtualWorkspaceProvider.ProvidesFastCopyOnWrite;
                }
                else
                {
                    return _physicalWorkspaceProvider.ProvidesFastCopyOnWrite;
                }
            }
        }

        public Task<IWorkspace> GetWorkspaceAsync(IWorkspaceDescriptor workspaceDescriptor, CancellationToken cancellationToken)
        {
            if (UseWorkspaceVirtualisation)
            {
                return _virtualWorkspaceProvider.GetWorkspaceAsync(workspaceDescriptor, cancellationToken);
            }
            else
            {
                return _physicalWorkspaceProvider.GetWorkspaceAsync(workspaceDescriptor, cancellationToken);
            }
        }
    }
}
