using Microsoft.AspNetCore.SignalR;
using Io.Database;
using Io.Readers;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Io
{
    public class IoDataHub : Hub
    {
        private readonly IoDbContext _dbContext;
        private readonly IDashboardReader _dashboardReader;
        private readonly IHistoryReader _historyReader;
        private readonly IUtilizationReader _utilizationReader;
        private readonly IHealthReader _healthReader;

        public IoDataHub(
            IoDbContext dbContext,
            IDashboardReader dashboardReader,
            IHistoryReader historyReader,
            IUtilizationReader utilizationReader,
            IHealthReader healthReader)
        {
            _dbContext = dbContext;
            _dashboardReader = dashboardReader;
            _historyReader = historyReader;
            _utilizationReader = utilizationReader;
            _healthReader = healthReader;
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("DashboardUpdated", await _dashboardReader.ReadAsync());
            await Clients.Caller.SendAsync("HealthUpdated", await _healthReader.ReadAsync());
            await Clients.Caller.SendAsync("HistoryUpdated", await _historyReader.ReadAsync());
            await Clients.Caller.SendAsync("UtilizationUpdated", await _utilizationReader.ReadAsync());
        }

        public async Task ResetUtilizationData()
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UtilizationMinutes\";");

            await Clients.All.SendAsync("UtilizationUpdated", await _utilizationReader.ReadAsync());
        }
    }
}
