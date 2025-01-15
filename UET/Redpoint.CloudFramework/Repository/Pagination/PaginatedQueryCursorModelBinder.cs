namespace Redpoint.CloudFramework.Repository.Pagination
{
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using System;
    using System.Threading.Tasks;

    public class PaginatedQueryCursorModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var modelName = bindingContext.ModelName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None ||
                valueProviderResult.FirstValue == null)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);
            bindingContext.Result = ModelBindingResult.Success(new PaginatedQueryCursor(valueProviderResult.FirstValue));
            return Task.CompletedTask;
        }
    }
}
