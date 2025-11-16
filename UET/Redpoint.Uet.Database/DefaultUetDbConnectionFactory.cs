namespace Redpoint.Uet.Database
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.Uet.Database.Migrations;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultUetDbConnectionFactory : IUetDbConnectionFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IReservationManagerForUet? _reservationManagerForUet;

        public DefaultUetDbConnectionFactory(
            IServiceProvider serviceProvider,
            IReservationManagerForUet? reservationManagerForUet = null)
        {
            _serviceProvider = serviceProvider;
            _reservationManagerForUet = reservationManagerForUet;
        }

        public async Task<IUetDbConnection> ConnectToDefaultDatabaseAsync(CancellationToken cancellationToken)
        {
            if (_reservationManagerForUet == null)
            {
                throw new InvalidOperationException("Can't call ConnectToDefaultDatabaseAsync if the IReservationManagerForUet service is not available!");
            }

            var readyToReturn = false;
            var reservation = await _reservationManagerForUet.ReserveExactAsync("UetDatabase", cancellationToken, hold: true);
            try
            {
                var connection = new DefaultUetDbConnection(
                    _serviceProvider.GetServices<IMigration>(),
                    Path.Combine(reservation.ReservedPath, "uet.db"));
                connection.Reservation = reservation;
                await connection.ConnectAsync(cancellationToken);
                readyToReturn = true;
                return connection;
            }
            finally
            {
                if (!readyToReturn)
                {
                    await reservation.DisposeAsync();
                }
            }
        }

        public async Task<IUetDbConnection> ConnectToSpecificDatabaseFileAsync(string databasePath, CancellationToken cancellationToken)
        {
            var connection = new DefaultUetDbConnection(
                _serviceProvider.GetServices<IMigration>(),
                databasePath);
            await connection.ConnectAsync(cancellationToken);
            return connection;
        }
    }
}
