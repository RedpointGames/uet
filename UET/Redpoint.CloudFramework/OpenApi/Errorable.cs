namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using System.Net;
    using System.Threading.Tasks;

    public class Errorable<T> : IActionResult where T : class
    {
        private int _statusCode = 200;

        public static Errorable<T> FromError(HttpStatusCode status, string message)
        {
            return new Errorable<T>(null, status, message);
        }

        public static Errorable<T> FromObject(T value)
        {
            return new Errorable<T>(value, null, null);
        }

        public static implicit operator Errorable<T>(T value) => FromObject(value);
        public static Errorable<T> FromT(T value) => FromObject(value);

        private Errorable(T? value, HttpStatusCode? statusCode, string? errorMessage)
        {
            Value = value;
            ErrorMessage = errorMessage;

            if (errorMessage != null && statusCode != null)
            {
                if (statusCode != null)
                {
                    _statusCode = (int)statusCode.Value;
                }
                else
                {
                    _statusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                _statusCode = (int)HttpStatusCode.OK;
            }
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var services = context.HttpContext.RequestServices;
            var executor = services.GetRequiredService<IActionResultExecutor<JsonResult>>();

            // @todo: What we really want here is the default serializer for the
            // value type, plus an additional errorMessage property.
            return executor.ExecuteAsync(context, new JsonResult(this)
            {
                StatusCode = _statusCode,
            });
        }

        public T? Value { get; }

        public string? ErrorMessage { get; }

        public T EnsureValue()
        {
            if (ErrorMessage != null)
            {
                throw new ErrorableException(ErrorMessage)
                {
                    StatusCode = _statusCode,
                };
            }

            return Value!;
        }
    }
}
