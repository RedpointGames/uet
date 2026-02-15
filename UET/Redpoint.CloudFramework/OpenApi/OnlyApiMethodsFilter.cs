namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class OnlyApiMethodsFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(swaggerDoc);
            ArgumentNullException.ThrowIfNull(context);

            foreach (var apiDescription in context.ApiDescriptions)
            {
                if (((ControllerActionDescriptor)apiDescription.ActionDescriptor).MethodInfo.GetCustomAttributes(typeof(ApiAttribute), false).Length == 0)
                {
                    var key = "/" + apiDescription.RelativePath?.TrimEnd('/');
                    swaggerDoc.Paths.Remove(key);
                }
            }
        }
    }
}
