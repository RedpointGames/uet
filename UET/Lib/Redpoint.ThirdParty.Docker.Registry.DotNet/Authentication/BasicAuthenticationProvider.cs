namespace Docker.Registry.DotNet.Authentication
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    [PublicAPI]
    public class BasicAuthenticationProvider : AuthenticationProvider
    {
        private readonly string _password;

        private readonly string _username;

        public BasicAuthenticationProvider(string username, string password)
        {
            this._username = username;
            this._password = password;
        }

        private static string Schema { get; } = "Basic";

        public override Task AuthenticateAsync(HttpRequestMessage request)
        {
            return Task.CompletedTask;
        }

        public override Task AuthenticateAsync(
            HttpRequestMessage request,
            HttpResponseMessage response)
        {
            this.TryGetSchemaHeader(response, Schema);

            var passBytes = Encoding.UTF8.GetBytes($"{this._username}:{this._password}");
            var base64Pass = Convert.ToBase64String(passBytes);

            //Set the header
            request.Headers.Authorization =
                new AuthenticationHeaderValue(Schema, base64Pass);

            return Task.CompletedTask;
        }
    }
}