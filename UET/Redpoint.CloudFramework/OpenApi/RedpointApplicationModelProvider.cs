#nullable enable

namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using System.Threading.Tasks;

    public class RedpointApplicationModelProvider : IApplicationModelProvider
    {
        public int Order => 200;

        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
        }

        public static System.Type NormalizePotentialAsyncType(System.Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type.IsConstructedGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return type.GetGenericArguments()[0];
                }
                if (type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }
            return type;
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            foreach (var controller in context.Result.Controllers)
            {
                foreach (var action in controller.Actions)
                {
                    var returnType = NormalizePotentialAsyncType(action.ActionMethod.ReturnType);

                    if (returnType.IsConstructedGenericType &&
                        returnType.GetGenericTypeDefinition() == typeof(Errorable<>))
                    {
                        action.Filters.Add(new ProducesResponseTypeAttribute(returnType, StatusCodes.Status200OK));
                        action.Filters.Add(new ProducesResponseTypeAttribute(returnType, StatusCodes.Status400BadRequest));
                        action.Filters.Add(new ProducesResponseTypeAttribute(returnType, StatusCodes.Status404NotFound));
                        action.Filters.Add(new ProducesResponseTypeAttribute(returnType, StatusCodes.Status500InternalServerError));
                    }
                }
            }
        }
    }
}
