namespace Redpoint.GrpcPipes
{
    using System;

    /// <summary>
    /// Configures how a call made via <see cref="IRetryableGrpc"/> should behave.
    /// </summary>
    public record class GrpcRetryConfiguration
    {
        /// <summary>
        /// The maximum time to allow for the request to complete, or for streaming calls, the maximum time to allow until we get the first response.
        /// </summary>
        public required TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// For streaming calls, the maximum time to allow between responses. If not set, the <see cref="RequestTimeout"/> is used.
        /// </summary>
        public TimeSpan? IdleTimeout { get; set; }

        /// <summary>
        /// The maximum number of attempts to retry the call. Defaults to 5 attempts.
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// The initial backoff for retries in milliseconds. Defaults to 1 second.
        /// </summary>
        public int InitialBackoffMilliseconds { get; set; } = 1000;

        /// <summary>
        /// The maximum backoff for retries in milliseconds. Defaults to 5 seconds.
        /// </summary>
        public int MaximumBackoffMilliseconds { get; set; } = 5000;

        /// <summary>
        /// The multiplier for the backoff after each failed request.
        /// </summary>
        public float ExponentialBackoffMultiplier { get; set; } = 1.5f;
    }
}
