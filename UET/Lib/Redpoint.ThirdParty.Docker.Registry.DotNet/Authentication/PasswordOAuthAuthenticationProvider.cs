namespace Docker.Registry.DotNet.Authentication
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.OAuth;

    using JetBrains.Annotations;

    [PublicAPI]
    public class PasswordOAuthAuthenticationProvider : AuthenticationProvider
    {
        private readonly OAuthClient _client = new OAuthClient();

        private readonly string _password;

        private readonly string _username;

        public PasswordOAuthAuthenticationProvider(string username, string password)
        {
            this._username = username;
            this._password = password;
        }

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
                            //Also include the repository(plugin) resource type to be able to access plugin repositories.
                            //See https://docs.docker.com/registry/spec/auth/scope/
                            bearerBits.Scope + " " + (bearerBits.Scope.Replace("repository:", "repository(plugin):")),
                            this._username,
                            this._password);

            //Set the header
            request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.AccessToken);
        }
    }
}