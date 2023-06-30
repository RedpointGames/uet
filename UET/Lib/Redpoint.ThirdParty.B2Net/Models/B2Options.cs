namespace B2Net.Models
{
    using System;
    using System.Threading;

    public class B2Options
    {
        public string AccountId { get; private set; }
        public string KeyId { get; set; }
        public string ApplicationKey { get; set; }
        public string BucketId { get; set; }
        /// <summary>
        /// Setting this to true will use this bucket by default for all
        /// api calls made from this client. Useful if your app will
        /// only ever use one bucket. Default: false.
        /// </summary>
        public bool PersistBucket { get; set; }

        // State
        public string ApiUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string AuthorizationToken { get; set; }
        public B2Capabilities Capabilities { get; private set; }
        /// <summary>
        /// This will only be set after a call to the upload API
        /// </summary>
        public string UploadAuthorizationToken { get; set; }
        public long RecommendedPartSize { get; set; }
        public long AbsoluteMinimumPartSize { get; set; }

        /// <summary>
        /// Deprecated: Will always be the same as RecommendedPartSize
        /// </summary>
        public long MinimumPartSize
        {
            get { return RecommendedPartSize; }
        }

        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// For operations that transfer data (such as uploads and downloads), this
        /// is the timeout used instead of <see cref="RequestTimeout"/>. Defaults
        /// to <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </summary>
        public TimeSpan DataTransferRequestTimeout { get; set; }

        public bool Authenticated { get; private set; }

        public B2Options()
        {
            PersistBucket = false;
            RequestTimeout = TimeSpan.FromSeconds(100);
            DataTransferRequestTimeout = Timeout.InfiniteTimeSpan;
        }

        public void SetState(B2AuthResponse response)
        {
            ApiUrl = response.apiUrl;
            DownloadUrl = response.downloadUrl;
            AuthorizationToken = response.authorizationToken;
            RecommendedPartSize = response.recommendedPartSize;
            AbsoluteMinimumPartSize = response.absoluteMinimumPartSize;
            AccountId = response.accountId;
            Capabilities = new B2Capabilities(response.allowed);
            Authenticated = true;
        }
    }
}
