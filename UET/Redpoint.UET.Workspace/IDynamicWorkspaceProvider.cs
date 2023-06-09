namespace Redpoint.UET.Workspace
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public interface IDynamicWorkspaceProvider : IWorkspaceProviderBase
    {
        bool UseWorkspaceVirtualisation { get; set; }
    }
}
