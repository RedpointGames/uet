namespace Io.Mappers
{
    using Io.Database;
    using Io.Database.Entities;
    using Io.Json.Api;
    using Io.Json.GitLab;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class MapperServiceRegistration
    {
        public static void AddMappers(this IServiceCollection services)
        {
            services.AddScoped<IMapper<BridgeJson, BuildEntity>, BridgeMapper>();
            services.AddScoped<IMapper<BuildWebhookJson, BuildEntity>, BuildWebhookMapper>();
            services.AddScoped<IMapper<PipelineWebhookJson, PipelineEntity>, PipelineWebhookMapper>();
            services.AddScoped<IMapper<TestJsonWithValidatedContext, TestEntity[]>, TestMapper>();
        }
    }
}
