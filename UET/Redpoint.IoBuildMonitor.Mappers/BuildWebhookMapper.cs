namespace Io.Mappers
{
    using Io.Database;
    using Io.Json.GitLab;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Redpoint.IoBuildMonitor.Mappers;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BuildWebhookMapper : IMapper<BuildWebhookJson, BuildEntity>
    {
        private readonly IoDbContext _db;

        public BuildWebhookMapper(IoDbContext db)
        {
            _db = db;
        }

        public async Task<BuildEntity?> Map(BuildWebhookJson? source, MapperContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return await _db.Builds.FromSqlInterpolated($"SELECT * FROM map_build_webhook({JsonSerializer.Serialize(source, IoJsonSerializerContext.Default.BuildWebhookJson)}::jsonb,{context.WebhookEventId}, {context.WebhookReceivedAt})").OrderBy(x => x.Id).FirstOrDefaultAsync();
        }
    }
}
