namespace B2Net.Http
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public static class HttpClientFactory
    {
        public static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var handler = new HttpClientHandler() { AllowAutoRedirect = true };

            var client = new HttpClient(handler);

            client.Timeout = timeout;

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
