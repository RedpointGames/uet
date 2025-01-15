namespace Redpoint.CloudFramework.Tracing
{
    using Sentry;

    public class SentryManagedTracer : IManagedTracer
    {
        private readonly IHub _hub;

        public SentryManagedTracer(IHub hub)
        {
            _hub = hub;
        }

        public ISpan StartSpan(string name, string? description)
        {
            var sentrySpanObject = _hub.GetSpan()?.StartChild(name, description);
            if (sentrySpanObject != null)
            {
                return new SentrySpan(sentrySpanObject);
            }
            return NullSpan._instance;
        }

        private class SentrySpan : ISpan
        {
            private readonly Sentry.ISpan _span;

            public SentrySpan(Sentry.ISpan span)
            {
                _span = span;
            }

            public void SetTag(string key, string value)
            {
                _span.SetTag(key, value);
            }

            public void SetExtra(string key, object? value)
            {
                _span.SetExtra(key, value);
            }

            public void Dispose()
            {
                _span.Finish();
            }
        }
    }
}
