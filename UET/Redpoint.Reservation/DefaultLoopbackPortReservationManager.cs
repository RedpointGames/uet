namespace Redpoint.Reservation
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;

    internal sealed class DefaultLoopbackPortReservationManager : ILoopbackPortReservationManager
    {
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;

        public DefaultLoopbackPortReservationManager(
            IGlobalMutexReservationManager globalMutexReservationManager)
        {
            _globalMutexReservationManager = globalMutexReservationManager;
        }

        public async ValueTask<ILoopbackPortReservation> ReserveAsync()
        {
            do
            {
                var pid = Environment.ProcessId;
                var pidUpper = (byte)((pid & 0xFF00) >> 8);
                var pidLower = (byte)(pid & 0x00FF);
                var rand = (byte)(Random.Shared.Next() & 0xFF);
                var loopbackAddress = new IPAddress(new byte[]
                {
                    127,
                    pidUpper,
                    pidLower,
                    rand,
                });
                var port = (ushort)(Random.Shared.Next(20000, 49151) & 0xFFFF);
                var endpoint = new IPEndPoint(loopbackAddress, port);

                var reservation = await _globalMutexReservationManager.TryReserveExactAsync(
                    $"RedpointReservation_{endpoint}").ConfigureAwait(false);
                if (reservation != null)
                {
                    // We reserved this port.
                    return new DefaultLoopbackPortReservation(endpoint, reservation);
                }

                await Task.Yield();
            }
            while (true);
        }
    }
}
