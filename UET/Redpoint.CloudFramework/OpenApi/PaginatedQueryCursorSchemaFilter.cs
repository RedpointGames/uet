namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi.Models;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Swashbuckle.AspNetCore.SwaggerGen;

    internal class PaginatedQueryCursorSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(PaginatedQueryCursor))
            {
                schema.Properties = null;
                schema.Type = "string";
            }
        }
    }
}
