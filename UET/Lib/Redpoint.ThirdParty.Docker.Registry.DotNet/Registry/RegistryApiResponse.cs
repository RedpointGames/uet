namespace Docker.Registry.DotNet.Registry
{
    using System.Net;
    using System.Net.Http.Headers;

    internal abstract class RegistryApiResponse
    {
        protected RegistryApiResponse(HttpStatusCode statusCode, HttpResponseHeaders headers)
        {
            this.StatusCode = statusCode;
            this.Headers = headers;
        }

        public HttpStatusCode StatusCode { get; }

        public HttpResponseHeaders Headers { get; }
    }

    internal class RegistryApiResponse<TBody> : RegistryApiResponse
    {
        internal RegistryApiResponse(
            HttpStatusCode statusCode,
            TBody body,
            HttpResponseHeaders headers)
            : base(statusCode, headers)
        {
            this.Body = body;
        }

        public TBody Body { get; }
    }
}