namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Reflection;

    internal class ExcludeSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetCustomAttribute<ExcludeSchemaAttribute>() != null)
            {
                ((OpenApiSchema)schema).Deprecated = true;
            }
        }
    }
}
