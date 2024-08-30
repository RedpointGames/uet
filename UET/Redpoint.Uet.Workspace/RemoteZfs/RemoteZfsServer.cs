namespace Redpoint.Uet.Workspace.RemoteZfs
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("linux")]
    public class RemoteZfsServer : RemoteZfs.RemoteZfsBase
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<RemoteZfsServer> _logger;
        private readonly string _zvolRoot;
        private readonly string _zvolSource;
        private readonly string _windowsShareNetworkPrefix;
        private long _requestId;

        public RemoteZfsServer(
            IProcessExecutor processExecutor,
            ILogger<RemoteZfsServer> logger,
            string zvolRoot,
            string zvolSource,
            string windowsShareNetworkPrefix)
        {
            _processExecutor = processExecutor;
            _logger = logger;
            _zvolRoot = zvolRoot;
            _zvolSource = zvolSource;
            _windowsShareNetworkPrefix = windowsShareNetworkPrefix;
        }

        public override async Task Acquire(
            IAsyncStreamReader<EmptyRequest> requestStream,
            IServerStreamWriter<AcquireResponse> responseStream,
            ServerCallContext context)
        {
            ArgumentNullException.ThrowIfNull(requestStream);
            ArgumentNullException.ThrowIfNull(responseStream);
            ArgumentNullException.ThrowIfNull(context);

            var id = $"rzfs-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-${_requestId++}";
            var didCreateSnapshot = false;
            var didCreateVolume = false;

            _logger.LogInformation($"Received acquire request, allocating with ID {id}...");

            try
            {
                // Snapshot the source.
                _logger.LogInformation($"Creating snapshot of source volume '{_zvolRoot}/{_zvolSource}@{id}'...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/sbin/zfs",
                        Arguments = ["snapshot", $"{_zvolRoot}/{_zvolSource}@{id}"]
                    },
                    CaptureSpecification.Passthrough,
                    context.CancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    return;
                }
                didCreateSnapshot = true;

                // Create a new volume from the snapshot.
                _logger.LogInformation($"Cloning source volume snapshot '{_zvolRoot}/{_zvolSource}@{id}' to '{_zvolRoot}/{id}'...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/sbin/zfs",
                        Arguments = ["clone", $"{_zvolRoot}/{_zvolSource}@{id}", $"{_zvolRoot}/{id}"]
                    },
                    CaptureSpecification.Passthrough,
                    context.CancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    return;
                }
                didCreateVolume = true;

                // Tell the client where it can find the new snapshot.
                _logger.LogInformation($"Informing client it's snapshot is available at '{_windowsShareNetworkPrefix.TrimEnd('\\')}\\{id}'...");
                await responseStream.WriteAsync(
                    new AcquireResponse
                    {
                        WindowsSharePath = $"{_windowsShareNetworkPrefix.TrimEnd('\\')}\\{id}",
                    },
                    context.CancellationToken).ConfigureAwait(false);

                // Now wait for our client to disconnect - that indicates it's done.
                _logger.LogInformation($"Waiting for the client to disconnect...");
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(60000, context.CancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected.
                _logger.LogInformation($"Client closed request.");
            }
            finally
            {
                // If we created the volume, delete it.
                if (didCreateVolume)
                {
                    _logger.LogInformation($"Destroying volume '{_zvolRoot}/{id}'...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = "/sbin/zfs",
                            Arguments = ["destroy", "-f", $"{_zvolRoot}/{id}"]
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }

                // If we created the snapshot, delete it.
                if (didCreateSnapshot)
                {
                    _logger.LogInformation($"Destroying snapshot '{_zvolRoot}/{_zvolSource}@{id}'...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = "/sbin/zfs",
                            Arguments = ["destroy", $"{_zvolRoot}/{_zvolSource}@{id}"]
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}
