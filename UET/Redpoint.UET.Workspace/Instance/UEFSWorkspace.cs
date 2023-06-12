namespace Redpoint.UET.Workspace.Instance
{
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using static Uefs.UEFS;

    internal class UEFSWorkspace : IWorkspace
    {
        private readonly UEFSClient _uefsClient;
        private readonly string _mountId;
        private readonly VirtualisedWorkspaceOptions _workspaceOptions;
        private readonly IAsyncDisposable[] _reservations;
        private readonly ILogger _logger;
        private readonly string _loggerReleaseMessage;

        public UEFSWorkspace(
            UEFSClient uefsClient,
            string mountId,
            string workspacePath,
            VirtualisedWorkspaceOptions workspaceOptions,
            IAsyncDisposable[] reservations,
            ILogger logger,
            string loggerReleaseMessage)
        {
            _uefsClient = uefsClient;
            _mountId = mountId;
            Path = workspacePath;
            _workspaceOptions = workspaceOptions;
            _reservations = reservations;
            _logger = logger;
            _loggerReleaseMessage = loggerReleaseMessage;
        }

        public string Path { get; }

        public async ValueTask DisposeAsync()
        {
            if (_workspaceOptions.UnmountAfterUse)
            {
                _logger.LogInformation($"{_loggerReleaseMessage} (unmounting)");
                await _uefsClient.UnmountAsync(new Uefs.UnmountRequest
                {
                    MountId = _mountId
                }, deadline: DateTime.UtcNow.AddSeconds(60));
            }
            else
            {
                _logger.LogInformation($"{_loggerReleaseMessage} (not unmounting)");
            }
            foreach (var reservation in _reservations)
            {
                await reservation.DisposeAsync();
            }
        }
    }
}
