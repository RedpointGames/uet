namespace Redpoint.CloudFramework.Tracing
{
    internal class SentryManagedTracer : IManagedTracer
    {
        private class SentrySpan : ISpan
        {
            private readonly Sentry.ISpan _span;

            public SentrySpan(Sentry.ISpan span)
            {
                _span = span;
            }

            public string DisplayName
            {
                get => _span.Description ?? string.Empty;
                set
                {
                    _span.Description = value;
                    _span.SetTag("description", value);
                    _span.SetData("description", value);
                }
            }

            public void Dispose()
            {
                _span.Finish();
            }

            public void SetTag(string key, string value)
            {
                _span.SetTag(key, value);
                _span.SetData(key, value);
            }
        }

        public ISpan StartSpan(string name, string? description = null)
        {
            var currentSpan = SentrySdk.GetSpan();
            if (currentSpan != null)
            {
                return new SentrySpan(currentSpan.StartChild(name, description ?? string.Empty));
            }

            var currentTransaction = SentrySdk.GetTransaction();
            if (currentTransaction != null)
            {
                return new SentrySpan(currentTransaction.StartChild(name, description ?? string.Empty));
            }

            return new SentrySpan(SentrySdk.StartSpan(name, description ?? string.Empty));
        }
    }
}
