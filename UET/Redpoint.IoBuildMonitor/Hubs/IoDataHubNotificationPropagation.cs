using Microsoft.AspNetCore.SignalR;
using Io.Readers;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Io.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Io
{
    public class IoDataHubNotificationPropagation : IHostedService
    {
        private readonly INotificationHub _notificationHub;
        private readonly IHubContext<IoDataHub> _hubContext;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private long? _handle;

        public IoDataHubNotificationPropagation(
            INotificationHub notificationHub,
            IHubContext<IoDataHub> hubContext,
            IServiceScopeFactory serviceScopeFactory)
        {
            _notificationHub = notificationHub;
            _hubContext = hubContext;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _handle = await _notificationHub.RegisterForNotifyChanges(async (type) =>
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    if (type == NotificationType.DashboardUpdated)
                    {
                        await _hubContext.Clients.All.SendAsync("DashboardUpdated", await scope.ServiceProvider.GetRequiredService<IDashboardReader>().ReadAsync());
                    }
                    else if (type == NotificationType.HealthUpdated)
                    {
                        await _hubContext.Clients.All.SendAsync("HealthUpdated", await scope.ServiceProvider.GetRequiredService<IHealthReader>().ReadAsync());
                    }
                    else if (type == NotificationType.HistoryUpdated)
                    {
                        await _hubContext.Clients.All.SendAsync("HistoryUpdated", await scope.ServiceProvider.GetRequiredService<IHistoryReader>().ReadAsync());
                    }
                    else if (type == NotificationType.UtilizationUpdated)
                    {
                        await _hubContext.Clients.All.SendAsync("UtilizationUpdated", await scope.ServiceProvider.GetRequiredService<IUtilizationReader>().ReadAsync());
                    }
                }
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_handle != null)
            {
                await _notificationHub.UnregisterForNotifyChanges(_handle.Value);
                _handle = null;
            }
        }
    }
}
