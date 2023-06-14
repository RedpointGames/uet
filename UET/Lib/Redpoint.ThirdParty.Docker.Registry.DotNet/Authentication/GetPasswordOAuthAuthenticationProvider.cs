namespace Docker.Registry.DotNet.Authentication
{
    using Docker.Registry.DotNet.OAuth;
    using System.Net.Http.Headers;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class GetPasswordOAuthAuthenticationProvider : AuthenticationProvider
    {
        private readonly OAuthGetClient _client = new OAuthGetClient();

        private string _cachedToken;

        private readonly string _password;

        private readonly string _username;

        public GetPasswordOAuthAuthenticationProvider(string username, string password)
        {
            this._username = username;
            this._password = password;
            this._cachedToken = null;
        }

        private static string Schema { get; } = "Bearer";

        public override Task AuthenticateAsync(HttpRequestMessage request)
        {
            if (this._cachedToken != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(Schema, this._cachedToken);
            }
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
                            bearerBits.Scope,
                            this._username,
                            this._password);

            //Set the header
            this._cachedToken = token.Token;
            request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.Token);
        }
    }
}
