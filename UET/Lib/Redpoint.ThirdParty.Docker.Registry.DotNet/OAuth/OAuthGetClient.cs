namespace Docker.Registry.DotNet.OAuth
{
    using Docker.Registry.DotNet.Helpers;
    using System;
    using System.Net.Http.Headers;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class OAuthGetClient
    {
        private readonly HttpClient _client = new HttpClient();

        private async Task<OAuthToken> GetTokenInnerAsync(
            string realm,
            string service,
            string scope,
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            HttpRequestMessage request;

            var queryString = new QueryString();

            queryString.AddIfNotEmpty("service", service);
            queryString.AddIfNotEmpty("scope", scope);
            queryString.AddIfNotEmpty("account", username);
            queryString.AddIfNotEmpty("client_id", "docker");
            queryString.AddIfNotEmpty("offline_token", "true");

            var builder = new UriBuilder(new Uri(realm))
            {
                Query = queryString.GetQueryString()
            };

            request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

            using (var response = await this._client.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new UnauthorizedAccessException("Unable to authenticate.");

                var body = await response.Content.ReadAsStringAsync();

                var token = System.Text.Json.JsonSerializer.Deserialize(body, DockerJsonSerializerContext.WithSettings.OAuthToken);

                return token;
            }
        }

        public Task<OAuthToken> GetTokenAsync(
            string realm,
            string service,
            string scope,
            CancellationToken cancellationToken = default)
        {
            return this.GetTokenInnerAsync(realm, service, scope, null, null, cancellationToken);
        }

        public Task<OAuthToken> GetTokenAsync(
            string realm,
            string service,
            string scope,
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            return this.GetTokenInnerAsync(
                realm,
                service,
                scope,
                username,
                password,
                cancellationToken);
        }
    }
}
