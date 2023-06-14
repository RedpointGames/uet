namespace Docker.Registry.DotNet.Registry
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;

    public class RegistryApiException : Exception
    {
        internal RegistryApiException(RegistryApiResponse response)
            : base($"Docker API responded with status code={response.StatusCode}")
        {
            this.StatusCode = response.StatusCode;
            this.Headers = response.Headers;
        }

        public HttpStatusCode StatusCode { get; }

        public HttpResponseHeaders Headers { get; }
    }
}