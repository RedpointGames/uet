namespace Redpoint.CloudFramework.OpenApi
{
    using System;

    public class JsonOptionsNotConfiguredException : Exception
    {
        public JsonOptionsNotConfiguredException()
            : base("Expected AddJsonOptionsForSwaggerReactApp to have been called on the MVC builder before calling AddSwaggerGenForReactApp.")
        {
        }
    }
}
