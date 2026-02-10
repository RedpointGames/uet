namespace Io.Mappers
{
    using Io.Database;
    using Io.Json.GitLab;
    using Microsoft.EntityFrameworkCore;
    using Redpoint.IoBuildMonitor.Mappers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BridgeMapper : IMapper<BridgeJson, BuildEntity>
    {
        private readonly IoDbContext _db;

        public BridgeMapper(IoDbContext db)
        {
            _db = db;
        }

        public async Task<BuildEntity?> Map(BridgeJson? source, MapperContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return await _db.Builds.FromSqlInterpolated($"SELECT * FROM map_bridge({JsonSerializer.Serialize(source, IoJsonSerializerContext.Default.BridgeJson)}::jsonb,{context.WebhookEventId})").OrderBy(x => x.Id).FirstOrDefaultAsync();
        }
    }
}
