namespace Redpoint.Uet.Workspace.Instance
{
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Reservation;
    using System.Threading.Tasks;

    internal class HandleWorkspace : IWorkspace
    {
        private readonly string _path;
        private readonly SafeFileHandle _handle;

        public HandleWorkspace(string path, SafeFileHandle handle)
        {
            _path = path;
            _handle = handle;
        }

        public string Path => _path;

        public ValueTask DisposeAsync()
        {
            _handle.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
