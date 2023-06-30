namespace B2Net
{
    using System;
    using B2Net.Http;
    using B2Net.Models;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text.Json;

    public class B2Client : IB2Client
    {
        private B2Options _options;
        private B2Capabilities _capabilities { get; set; }

        public B2Capabilities Capabilities
        {
            get
            {
                if (_options.Authenticated)
                {
                    return _capabilities;
                }
                else
                {
                    throw new NotAuthorizedException("You attempted to load the cabapilities of this key before authenticating with Backblaze. You must Authorize before you can access Capabilities.");
                }
            }
        }

        /// <summary>
        /// If you specify authorizeOnInitialize = false, you MUST call Initialize() once before you use the client.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="authorizeOnInitialize"></param>
        public B2Client(B2Options options, bool authorizeOnInitialize = true)
        {
            // Should we authorize on the class initialization?
            if (authorizeOnInitialize)
            {
                _options = Authorize(options);
                Buckets = new Buckets(options);
                Files = new Files(options);
                LargeFiles = new LargeFiles(options);
                _capabilities = options.Capabilities;
            }
            else
            {
                // If not, then the user will have to Initialize() before making any calls.
                _options = options;
            }
        }

        /// <summary>
        /// Simple method for instantiating the B2Client. Does auth for you. See https://www.backblaze.com/b2/docs/application_keys.html for details on application keys.
        /// This method defaults to not persisting a bucket. Manually build the options object if you wish to do that.
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="applicationkey"></param>
        /// <param name="requestTimeout"></param>
        public B2Client(string keyId, string applicationkey, TimeSpan? requestTimeout)
        {
            _options = new B2Options()
            {
                KeyId = keyId,
                ApplicationKey = applicationkey,
                RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(100)
            };
            _options = Authorize(_options);

            Buckets = new Buckets(_options);
            Files = new Files(_options);
            LargeFiles = new LargeFiles(_options);
            _capabilities = _options.Capabilities;
        }

        /// <summary>
        /// Simple method for instantiating the B2Client. Does auth for you. See https://www.backblaze.com/b2/docs/application_keys.html for details on application keys.
        /// This method defaults to not persisting a bucket. Manually build the options object if you wish to do that.
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="applicationkey"></param>
        /// <param name="keyId"></param>
        /// <param name="requestTimeout"></param>
        [Obsolete("Use B2Client(string keyId, string applicationkey, int requestTimeout = 100) instead as AccountId is no longer needed")]
        public B2Client(string accountId, string applicationkey, string keyId, TimeSpan? requestTimeout) : this(keyId, applicationkey, requestTimeout)
        {
        }

        public IBuckets Buckets { get; private set; }
        public IFiles Files { get; private set; }
        public ILargeFiles LargeFiles { get; private set; }

        /// <summary>
        /// Only call this method if you created a B2Client with authorizeOnInitalize = false. This method of using B2.NET is considered in Beta, as it has not been extensively tested.
        /// </summary>
        /// <returns></returns>
        public Task Initialize()
        {
            _options = Authorize(_options);
            Buckets = new Buckets(_options);
            Files = new Files(_options);
            LargeFiles = new LargeFiles(_options);
            _capabilities = _options.Capabilities;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Authorize against the B2 storage service. Requires that KeyId and ApplicationKey on the options object be set.
        /// </summary>
        /// <returns>B2Options containing the download url, new api url, AccountID and authorization token.</returns>
        public async Task<B2Options> Authorize(CancellationToken cancelToken = default(CancellationToken))
        {
            return await AuthorizeAsync(_options);
        }

        public static async Task<B2Options> AuthorizeAsync(string keyId, string applicationkey)
        {
            return await AuthorizeAsync(new B2Options() { ApplicationKey = applicationkey, KeyId = keyId });
        }

        public static B2Options Authorize(string keyId, string applicationkey)
        {
            return Authorize(new B2Options() { ApplicationKey = applicationkey, KeyId = keyId });
        }

        /// <summary>
        /// Requires that KeyId and ApplicationKey on the options object be set. If you are using an application key you must specify the accountId, the keyId, and the applicationKey.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async Task<B2Options> AuthorizeAsync(B2Options options)
        {
            // Return if already authenticated.
            if (options.Authenticated)
            {
                return options;
            }

            if (string.IsNullOrWhiteSpace(options.KeyId) || string.IsNullOrWhiteSpace(options.ApplicationKey))
            {
                throw new AuthorizationException("Either KeyId or ApplicationKey were not specified.");
            }

            var client = HttpClientFactory.CreateHttpClient(options.RequestTimeout);

            var requestMessage = AuthRequestGenerator.Authorize(options);
            var response = await client.SendAsync(requestMessage);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var authResponse = JsonSerializer.Deserialize<B2AuthResponse>(jsonResponse, B2JsonSerializerContext.B2Defaults.B2AuthResponse);

                options.SetState(authResponse);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Return a better exception because of confusing Keys api.
                throw new AuthorizationException("If you are using an Application key and not a Master key, make sure that you are supplying the Key ID and Key Value for that Application Key. Do not mix your Account ID with your Application Key.");
            }
            else
            {
                throw new AuthorizationException(jsonResponse);
            }

            return options;
        }

        /// <summary>
        /// Requires that KeyId and ApplicationKey on the options object be set. If you are using an application key you must specify the accountId, the keyId, and the applicationKey.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static B2Options Authorize(B2Options options)
        {
            return AuthorizeAsync(options).Result;
        }
    }
}
