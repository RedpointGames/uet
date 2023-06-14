namespace Docker.Registry.DotNet.OAuth
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Helpers;

    internal class OAuthClient
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

            if (username == null || password == null)
            {
                var queryString = new QueryString();

                queryString.AddIfNotEmpty("service", service);
                queryString.AddIfNotEmpty("scope", scope);

                var builder = new UriBuilder(new Uri(realm))
                {
                    Query = queryString.GetQueryString()
                };

                request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Post, realm)
                {
                    Content = new FormUrlEncodedContent(
                        new Dictionary<string, string>()
                        {
                            {"client_id", "Docker.Registry.DotNet"},
                            {"grant_type", "password"},
                            {"username", username},
                            {"password", password},
                            {"service", service},
                            {"scope", scope},
                        }
                    ),
                };
            }

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