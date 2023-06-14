namespace Docker.Registry.DotNet.Registry
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Authentication;
    using Docker.Registry.DotNet.Helpers;

    internal class NetworkClient : IDisposable
    {
        private const string UserAgent = "Docker.Registry.DotNet";

        private static readonly TimeSpan InfiniteTimeout =
            TimeSpan.FromMilliseconds(Timeout.Infinite);

        private readonly AuthenticationProvider _authenticationProvider;

        private readonly HttpClient _client;

        private readonly RegistryClientConfiguration _configuration;

        private readonly IEnumerable<Action<RegistryApiResponse>> _errorHandlers =
            new Action<RegistryApiResponse>[]
            {
                r =>
                {
                    if (r.StatusCode == HttpStatusCode.Unauthorized)
                        throw new UnauthorizedApiException(r);
                }
            };

        private Uri _effectiveEndpointBaseUri;

        public NetworkClient(
            RegistryClientConfiguration configuration,
            AuthenticationProvider authenticationProvider)
        {
            this._configuration =
                configuration ?? throw new ArgumentNullException(nameof(configuration));

            this._authenticationProvider = authenticationProvider
                                           ?? throw new ArgumentNullException(
                                               nameof(authenticationProvider));
            if (configuration.HttpMessageHandler is null)
                this._client = new HttpClient();
            else
                this._client = new HttpClient(configuration.HttpMessageHandler);

            this.DefaultTimeout = configuration.DefaultTimeout;

            this.JsonSerializer = new JsonSerializer();

            if (this._configuration.EndpointBaseUri != null)
                this._effectiveEndpointBaseUri = this._configuration.EndpointBaseUri;
        }

        public TimeSpan DefaultTimeout { get; set; }

        public JsonSerializer JsonSerializer { get; }

        public void Dispose()
        {
            this._client?.Dispose();
        }

        /// <summary>
        ///     Ensures that we have configured (and potentially probed) the end point.
        /// </summary>
        /// <returns></returns>
        private async Task EnsureConnection()
        {
            if (this._effectiveEndpointBaseUri != null) return;

            var tryUrls = new List<string>();

            // clean up the host
            var host = this._configuration.Host.ToLower().Trim();

            if (host.StartsWith("http"))
            {
                // includes schema -- don't add
                tryUrls.Add(host);
            }
            else
            {
                tryUrls.Add($"https://{host}");
                tryUrls.Add($"http://{host}");
            }

            var exceptions = new List<Exception>();

            foreach (var url in tryUrls)
                try
                {
                    await this.ProbeSingleAsync($"{url}/v2/");
                    this._effectiveEndpointBaseUri = new Uri(url);
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

            throw new RegistryConnectionException(
                $"Unable to connect to any: {tryUrls.Select(s => $"'{s}/v2/'").ToDelimitedString(", ")}'",
                new AggregateException(exceptions));
        }

        private async Task ProbeSingleAsync(string uri)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (await this._client.SendAsync(request))
            {
            }
        }

        internal async Task<RegistryApiResponse<string>> MakeRequestAsync(
            CancellationToken cancellationToken,
            HttpMethod method,
            string path,
            IQueryString queryString = null,
            IDictionary<string, string> headers = null,
            Func<HttpContent> content = null)
        {
            //Console.WriteLine(
            //    "Requesting Path: {0} Method: {1} QueryString: {2}",
            //    path,
            //    method,
            //    queryString?.GetQueryString());

            using (var response = await this.InternalMakeRequestAsync(
                                      this.DefaultTimeout,
                                      HttpCompletionOption.ResponseContentRead,
                                      method,
                                      path,
                                      queryString,
                                      headers,
                                      content,
                                      cancellationToken))
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var apiResponse = new RegistryApiResponse<string>(
                    response.StatusCode,
                    responseBody,
                    response.Headers);

                this.HandleIfErrorResponse(apiResponse);

                return apiResponse;
            }
        }

        internal async Task<RegistryApiResponse<string>> MakeRequestNotErrorAsync(
            CancellationToken cancellationToken,
            HttpMethod method,
            string path,
            IQueryString queryString = null,
            IDictionary<string, string> headers = null,
            Func<HttpContent> content = null)
        {
            //Console.WriteLine(
            //    "Requesting Path: {0} Method: {1} QueryString: {2}",
            //    path,
            //    method,
            //    queryString?.GetQueryString());

            using (var response = await this.InternalMakeRequestAsync(
                                      this.DefaultTimeout,
                                      HttpCompletionOption.ResponseContentRead,
                                      method,
                                      path,
                                      queryString,
                                      headers,
                                      content,
                                      cancellationToken))
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var apiResponse = new RegistryApiResponse<string>(
                    response.StatusCode,
                    responseBody,
                    response.Headers);

                return apiResponse;
            }
        }

        internal async Task<RegistryApiResponse<Stream>> MakeRequestForStreamedResponseAsync(
            CancellationToken cancellationToken,
            HttpMethod method,
            string path,
            IQueryString queryString = null)
        {
            var response = await this.InternalMakeRequestAsync(
                               InfiniteTimeout,
                               HttpCompletionOption.ResponseHeadersRead,
                               method,
                               path,
                               queryString,
                               null,
                               null,
                               cancellationToken);

            var body = await response.Content.ReadAsStreamAsync();

            var apiResponse = new RegistryApiResponse<Stream>(
                response.StatusCode,
                body,
                response.Headers);

            this.HandleIfErrorResponse(apiResponse);

            return apiResponse;
        }

        private async Task<HttpResponseMessage> InternalMakeRequestAsync(
            TimeSpan timeout,
            HttpCompletionOption completionOption,
            HttpMethod method,
            string path,
            IQueryString queryString,
            IDictionary<string, string> headers,
            Func<HttpContent> content,
            CancellationToken cancellationToken)
        {
            await this.EnsureConnection();

            var request = this.PrepareRequest(method, path, queryString, headers, content);

            if (timeout != InfiniteTimeout)
            {
                var timeoutTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutTokenSource.CancelAfter(timeout);
                cancellationToken = timeoutTokenSource.Token;
            }

            await this._authenticationProvider.AuthenticateAsync(request);

            var response = await this._client.SendAsync(
                               request,
                               completionOption,
                               cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                //Prepare another request (we can't reuse the same request)
                var request2 = this.PrepareRequest(method, path, queryString, headers, content);

                //Authenticate given the challenge
                await this._authenticationProvider.AuthenticateAsync(request2, response);

                //Send it again
                response = await this._client.SendAsync(
                               request2,
                               completionOption,
                               cancellationToken);
            }

            return response;
        }

        internal void HandleIfErrorResponse(RegistryApiResponse response)
        {
            // If no customer handlers just default the response.
            foreach (var handler in this._errorHandlers) handler(response);

            // No custom handler was fired. Default the response for generic success/failures.
            if (response.StatusCode < HttpStatusCode.OK
                || response.StatusCode >= HttpStatusCode.BadRequest)
            {
                if (response is RegistryApiResponse<string> stringResponse)
                {
                    Console.WriteLine($"docker registry responded with status code {response.StatusCode}: {stringResponse.Body}");
                }

                throw new RegistryApiException(response);
            }
        }

        internal HttpRequestMessage PrepareRequest(
            HttpMethod method,
            string path,
            IQueryString queryString,
            IDictionary<string, string> headers,
            Func<HttpContent> content)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var request = new HttpRequestMessage(
                method,
                this._effectiveEndpointBaseUri.BuildUri(path, queryString));

            request.Headers.Add("User-Agent", UserAgent);
            request.Headers.AddRange(headers);

            //Create the content
            request.Content = content?.Invoke();

            return request;
        }
    }
}