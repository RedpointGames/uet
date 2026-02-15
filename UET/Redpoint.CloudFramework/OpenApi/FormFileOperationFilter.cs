namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System;
    using System.Linq;

    public class FormFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            var fileUploadMime = "multipart/form-data";
            if (operation.RequestBody == null || !operation.RequestBody.Content!.Any(x => x.Key.Equals(fileUploadMime, StringComparison.OrdinalIgnoreCase)))
                return;

            var fileParams = context.MethodInfo.GetParameters().Where(p => p.ParameterType == typeof(IFormFile));
            ((OpenApiSchema)operation.RequestBody.Content![fileUploadMime].Schema!).Properties =
                fileParams.ToDictionary(k => k.Name ?? string.Empty, v => (IOpenApiSchema)new OpenApiSchema()
                {
                    Type = JsonSchemaType.String,
                    Format = "binary"
                });
        }
    }
}
