namespace Redpoint.XunitFramework
{
    using Microsoft.Extensions.Hosting;
    using System;
    using Xunit;

    /// <summary>
    /// An implementation of <see cref="IHostApplicationLifetime" /> that stops when the test stops.
    /// </summary>
    public sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        /// <inheritdoc />
        public CancellationToken ApplicationStarted => TestContext.Current.CancellationToken;

        /// <inheritdoc />
        public CancellationToken ApplicationStopping => TestContext.Current.CancellationToken;

        /// <inheritdoc />
        public CancellationToken ApplicationStopped => TestContext.Current.CancellationToken;

        /// <inheritdoc />
        public void StopApplication()
        {
            TestContext.Current.CancelCurrentTest();
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
