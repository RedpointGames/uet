namespace Docker.Registry.DotNet.Authentication
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.OAuth;

    using JetBrains.Annotations;

    [PublicAPI]
    public class AnonymousOAuthAuthenticationProvider : AuthenticationProvider
    {
        private readonly OAuthClient _client = new OAuthClient();

        private static string Schema { get; } = "Bearer";

        public override Task AuthenticateAsync(HttpRequestMessage request)
        {
            return Task.CompletedTask;
        }

        public override async Task AuthenticateAsync(
            HttpRequestMessage request,
            HttpResponseMessage response)
        {
            var header = this.TryGetSchemaHeader(response, Schema);

            //Get the bearer bits
            var bearerBits = AuthenticateParser.ParseTyped(header.Parameter);

            //Get the token
            var token = await this._client.GetTokenAsync(
                            bearerBits.Realm,
                            bearerBits.Service,
                            bearerBits.Scope);

            //Set the header
            request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.Token);
        }
    }
}