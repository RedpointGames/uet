namespace Redpoint.CloudFramework.Infrastructure
{
    using Microsoft.Extensions.Logging;
    using Sentry.Extensibility;
    using Sentry.Http;
    using Sentry.Infrastructure;
    using Sentry.Protocol.Envelopes;
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class ResponseLoggingHttpTransport : HttpTransportBase, ITransport, IDisposable
    {
        private readonly HttpClient _httpClient;

        public ResponseLoggingHttpTransport(
            SentryOptions options,
            Func<string, string?>? getEnvironmentVariable = null,
            ISystemClock? clock = null)
            : base(options, getEnvironmentVariable, clock)
        {
            _httpClient = new HttpClient();
        }

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var processedEnvelope = ProcessEnvelope(envelope);
            if (processedEnvelope.Items.Count > 0)
            {
                using var request = CreateRequest(processedEnvelope);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                try
                {
                    await HandleResponseAsync(response, processedEnvelope, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    using var requestRetry = CreateRequest(processedEnvelope);
                    using var responseRetry = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    Console.Error.Write($"error: Received exception when processing Sentry response. Retried response was status code {response.StatusCode} with body: {await responseRetry.Content.ReadAsStringAsync(cancellationToken)}");

                    throw;
                }
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
