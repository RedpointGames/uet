namespace Redpoint.OpenGE.Component.Worker.PeerRemoteFs
{
    using Fsp;
    using Grpc.Net.Client;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Core;
    using Redpoint.Reservation;
    using Redpoint.Rfs.WinFsp;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultPeerRemoteFsManager : IPeerRemoteFsManager
    {
        private readonly ILogger<DefaultPeerRemoteFsManager> _logger;
        private readonly IReservationManagerForOpenGE _reservationManagerForOpenGE;
        private readonly Dictionary<IPEndPoint, PeerRemoteFsState> _currentPeerRemoteFs;
        private readonly MutexSlim _currentPeerRemoteFsLock;

        private class PeerRemoteFsState
        {
            public required IPEndPoint EndPoint { get; init; }
            public required WindowsRfsClient Fs { get; init; }
            public required FileSystemHost FsHost { get; init; }
            public required IReservation Reservation { get; init; }
            public int HandleCount { get; set; }
        }

        [SupportedOSPlatform("windows6.2")]
        private class PeerRemoteFsStateHandle : IPeerRemoteFsHandle
        {
            private readonly DefaultPeerRemoteFsManager _manager;
            private readonly PeerRemoteFsState _state;
            private readonly string[] _additionalReparsePoints;

            public PeerRemoteFsStateHandle(
                DefaultPeerRemoteFsManager manager,
                PeerRemoteFsState state,
                string[] additionalReparsePoints)
            {
                _manager = manager;
                _state = state;
                _additionalReparsePoints = additionalReparsePoints;
                _state.HandleCount++;
                _state.Fs.AddAdditionalReparsePoints(additionalReparsePoints);
            }

            public string Path => _state.Reservation.ReservedPath;

            public async ValueTask DisposeAsync()
            {
                using (await _manager._currentPeerRemoteFsLock.WaitAsync())
                {
                    _state.Fs.RemoveAdditionalReparsePoints(_additionalReparsePoints);
                    _state.HandleCount--;
                    if (_state.HandleCount == 0)
                    {
                        _state.FsHost.Dispose();
                        await _state.Reservation.DisposeAsync();
                        _manager._currentPeerRemoteFs.Remove(_state.EndPoint);
                    }
                }
            }
        }

        public DefaultPeerRemoteFsManager(
            ILogger<DefaultPeerRemoteFsManager> logger,
            IReservationManagerForOpenGE reservationManagerForOpenGE)
        {
            _logger = logger;
            _reservationManagerForOpenGE = reservationManagerForOpenGE;
            _currentPeerRemoteFs = new Dictionary<IPEndPoint, PeerRemoteFsState>();
            _currentPeerRemoteFsLock = new MutexSlim();
        }

        public async ValueTask<IPeerRemoteFsHandle> AcquirePeerRemoteFs(
            IPAddress ipAddress,
            int port,
            string[] additionalReparsePoints)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                throw new PlatformNotSupportedException("AcquirePeerRemoteFs can not be called on this platform.");
            }

            using (await _currentPeerRemoteFsLock.WaitAsync())
            {
                var endpoint = new IPEndPoint(ipAddress, port);
                if (_currentPeerRemoteFs.ContainsKey(endpoint))
                {
                    return new PeerRemoteFsStateHandle(this, _currentPeerRemoteFs[endpoint], additionalReparsePoints);
                }
                else
                {
                    var didSetup = false;
                    var reservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync("OpenGERemoteFs", endpoint.ToString());
                    try
                    {
                        var fsClient = new WindowsRfs.WindowsRfsClient(
                            GrpcChannel.ForAddress($"http://{(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ipAddress}]" : ipAddress)}:{port}"));
                        var fs = new WindowsRfsClient(_logger, fsClient);
                        var fsHost = new FileSystemHost(fs);
                        try
                        {
                            Directory.Delete(reservation.ReservedPath);
                            var mountResult = fsHost.Mount(reservation.ReservedPath);
                            if (mountResult < 0)
                            {
                                throw new IOException($"Failed to mount virtual filesystem via WinFsp: 0x{mountResult:X}", mountResult);
                            }
                            var state = new PeerRemoteFsState
                            {
                                EndPoint = endpoint,
                                Fs = fs,
                                FsHost = fsHost,
                                Reservation = reservation,
                            };
                            _currentPeerRemoteFs[endpoint] = state;
                            didSetup = true;
                            return new PeerRemoteFsStateHandle(this, state, additionalReparsePoints);
                        }
                        finally
                        {
                            if (!didSetup)
                            {
                                fsHost.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        if (!didSetup)
                        {
                            await reservation.DisposeAsync();
                        }
                    }
                }
            }
        }
    }
}
