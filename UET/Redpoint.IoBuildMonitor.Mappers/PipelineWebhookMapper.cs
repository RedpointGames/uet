namespace Io.Mappers
{
    using Io.Database;
    using Io.Json.GitLab;
    using Microsoft.EntityFrameworkCore;
    using Redpoint.IoBuildMonitor.Mappers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class PipelineWebhookMapper : IMapper<PipelineWebhookJson, PipelineEntity>
    {
        private readonly IoDbContext _db;

        public PipelineWebhookMapper(IoDbContext db)
        {
            _db = db;
        }

        public async Task<PipelineEntity?> Map(PipelineWebhookJson? source, MapperContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return await _db.Pipelines.FromSqlInterpolated($"SELECT * FROM map_pipeline_webhook({JsonSerializer.Serialize(source, IoJsonSerializerContext.Default.PipelineWebhookJson)}::jsonb,{context.WebhookEventId}, {context.WebhookReceivedAt})").OrderBy(x => x.Id).FirstOrDefaultAsync();
        }
    }
}
