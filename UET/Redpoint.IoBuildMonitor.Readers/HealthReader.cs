namespace Io.Readers
{
    using Io.Database;
    using Io.Json.Frontend;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IHealthReader
    {
        Task<HealthStats> ReadAsync();
    }

    public class HealthReader : IHealthReader
    {
        private readonly IoDbContext _db;

        public HealthReader(IoDbContext db)
        {
            _db = db;
        }

        public async Task<HealthStats> ReadAsync()
        {
            var stats = await _db.ProjectHealths.ToListAsync();

            return new HealthStats
            {
                ProjectHealthStats = stats.Select(x =>
                {
                    return new ProjectHealthStats
                    {
                        ProjectId = x.ProjectId,
                        Name = x.Name,
                        WebUrl = x.WebUrl,
                        DefaultBranch = x.DefaultBranch,
                        PipelineId = x.PipelineId,
                        Status = x.Status,
                        Sha = x.Sha,
                    };
                }).OrderByDescending(x =>
                {
                    switch (x.Status)
                    {
                        case "failed":
                            return 4;
                        case "running":
                        case "pending":
                        case "queued":
                            return 3;
                        case "canceled":
                            return 2;
                        default:
                            return 1;
                    }
                }).ThenBy(x => x.ProjectId).ToList(),
            };
        }
    }
}
