namespace Redpoint.UET.Workspace.Instance
{
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using static Uefs.UEFS;

    internal class UEFSWorkspace : IWorkspace
    {
        private readonly UEFSClient _uefsClient;
        private readonly string _mountId;
        private readonly IAsyncDisposable[] _reservations;
        private readonly ILogger _logger;
        private readonly string _loggerReleaseMessage;

        public UEFSWorkspace(
            UEFSClient uefsClient,
            string mountId,
            string workspacePath,
            IAsyncDisposable[] reservations,
            ILogger logger,
            string loggerReleaseMessage)
        {
            _uefsClient = uefsClient;
            _mountId = mountId;
            Path = workspacePath;
            _reservations = reservations;
            _logger = logger;
            _loggerReleaseMessage = loggerReleaseMessage;
        }

        public string Path { get; }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation($"{_loggerReleaseMessage} (unmounting)");
            await _uefsClient.UnmountAsync(new Uefs.UnmountRequest
            {
                MountId = _mountId
            }, deadline: DateTime.UtcNow.AddSeconds(60));
            foreach (var reservation in _reservations)
            {
                await reservation.DisposeAsync();
            }
        }
    }
}
