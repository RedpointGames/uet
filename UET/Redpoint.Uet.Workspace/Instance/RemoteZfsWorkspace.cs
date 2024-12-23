namespace Redpoint.Uet.Workspace.Instance
{
    using Grpc.Core;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace.RemoteZfs;
    using System.Threading.Tasks;

    internal class RemoteZfsWorkspace : IWorkspace
    {
        private readonly AsyncServerStreamingCall<AcquireWorkspaceResponse> _connection;
        private readonly IReservation _physicalReservation;

        public RemoteZfsWorkspace(
            AsyncServerStreamingCall<AcquireWorkspaceResponse> connection,
            IReservation physicalReservation)
        {
            _connection = connection;
            _physicalReservation = physicalReservation;
        }

        public string Path => System.IO.Path.Combine(_physicalReservation.ReservedPath, "S");

        public async ValueTask DisposeAsync()
        {
            if (Directory.Exists(System.IO.Path.Combine(_physicalReservation.ReservedPath, "S")))
            {
                Directory.Delete(System.IO.Path.Combine(_physicalReservation.ReservedPath, "S"));
            }

            await _physicalReservation.DisposeAsync().ConfigureAwait(false);

            _connection.Dispose();
        }
    }

}
