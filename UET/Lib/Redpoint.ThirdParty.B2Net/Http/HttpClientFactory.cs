namespace B2Net.Http
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public static class HttpClientFactory
    {
        private static HttpClient _client;

        public static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var client = _client;
            if (client == null)
            {
                var handler = new HttpClientHandler() { AllowAutoRedirect = true };

                client = new HttpClient(handler);

                client.Timeout = timeout;

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client = client;
            }
            return client;
        }
    }
}
