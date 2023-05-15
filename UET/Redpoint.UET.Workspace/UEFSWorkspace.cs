namespace Redpoint.UET.Workspace
{
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;

    internal class UEFSWorkspace : IWorkspace
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly string _uefsPath;

        public UEFSWorkspace(
            IProcessExecutor processExecutor,
            string uefsPath,
            string workspacePath)
        {
            _processExecutor = processExecutor;
            _uefsPath = uefsPath;
            Path = workspacePath;
        }

        public string Path { get; }

        public async ValueTask DisposeAsync()
        {
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = _uefsPath,
                    Arguments = new[]
                    {
                        "unmount",
                        "--dir",
                        Path
                    }
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);
        }
    }
}
