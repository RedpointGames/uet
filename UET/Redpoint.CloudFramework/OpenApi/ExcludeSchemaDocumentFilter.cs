namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Linq;

    internal class ExcludeSchemaDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (var kv in swaggerDoc.Components!.Schemas!.ToArray())
            {
                if (kv.Value.Deprecated)
                {
                    swaggerDoc.Components.Schemas!.Remove(kv.Key);
                }
            }
        }
    }
}
