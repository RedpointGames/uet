namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Linq;

    internal class RequiredSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema.Properties == null)
            {
                return;
            }

            var notNullableProperties = schema
                .Properties
                .Where(x => !schema.Required.Contains(x.Key))
                .ToList();

            foreach (var property in notNullableProperties)
            {
                schema.Required.Add(property.Key);
            }
        }
    }
}
