namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Reflection;

    internal class ExcludeSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetCustomAttribute<ExcludeSchemaAttribute>() != null)
            {
                schema.Deprecated = true;
            }
        }
    }
}
