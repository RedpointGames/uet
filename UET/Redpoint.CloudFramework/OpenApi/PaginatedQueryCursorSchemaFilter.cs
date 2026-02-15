namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Swashbuckle.AspNetCore.SwaggerGen;

    internal class PaginatedQueryCursorSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(PaginatedQueryCursor))
            {
                ((OpenApiSchema)schema).Properties = null;
                ((OpenApiSchema)schema).Type = JsonSchemaType.String;
            }
        }
    }
}
