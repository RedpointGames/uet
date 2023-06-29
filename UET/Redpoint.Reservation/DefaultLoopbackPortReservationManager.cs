namespace Redpoint.Reservation
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultLoopbackPortReservationManager : ILoopbackPortReservationManager
    {
        public async ValueTask<ILoopbackPortReservation> ReserveAsync()
        {
            do
            {
                var pid = Process.GetCurrentProcess().Id;
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

                var mutex = new Mutex(false, $"RedpointReservation_{endpoint}");
                if (mutex.WaitOne(0))
                {
                    // We reserved this port.
                    return new DefaultLoopbackPortReservation(endpoint, mutex);
                }

                await Task.Yield();
            }
            while (true);
        }
    }
}
