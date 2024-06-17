namespace Redpoint.Uet.BuildPipeline.MultiWorkspace
{
    using Redpoint.Uet.Workspace;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MultiWorkspace : IAsyncDisposable
    {
        private readonly IReadOnlyDictionary<string, IWorkspace> _workspaces;

        internal MultiWorkspace(
            IReadOnlyDictionary<string, IWorkspace> workspaces)
        {
            _workspaces = workspaces;
        }

        public IReadOnlyDictionary<string, IWorkspace> Workspaces => _workspaces;

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            foreach (var kv in _workspaces)
            {
                await kv.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
