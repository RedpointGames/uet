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
                set => _span.Description = value;
            }

            public void Dispose()
            {
                _span.Finish();
            }

            public void SetTag(string key, string value)
            {
                _span.SetData(key, value);
            }
        }

        public ISpan StartSpan(string name, string? description = null)
        {
            return new SentrySpan(SentrySdk.StartSpan(name, description ?? string.Empty));
        }
    }
}
