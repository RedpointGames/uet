namespace Redpoint.CloudFramework.Tracing
{
    using System.Xml.Linq;

    internal class SentryManagedTracer : IManagedTracer
    {
        private class SentrySpan : ICacheGetSpan, ICachePutSpan
        {
            private readonly Sentry.ISpan _span;
            private string _key;
            private bool _hit;
            private bool _write;

            public SentrySpan(Sentry.ISpan span)
            {
                _span = span;
                _key = string.Empty;
                _hit = false;
                _write = false;
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

            public bool Hit
            {
                get
                {
                    return _hit;
                }
                set
                {
                    _span.SetData("cache.hit", value);
                    _hit = value;
                }
            }

            public bool Write
            {
                get
                {
                    return _write;
                }
                set
                {
                    _span.SetData("cache.write", value);
                    _write = value;
                }
            }

            public string Key
            {
                get
                {
                    return _key ?? string.Empty;
                }
                set
                {
                    _span.SetData("cache.key", value);
                    _key = value;
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

        private static Sentry.ISpan CreateSentrySpan(string name, string? description = null)
        {
            var currentSpan = SentrySdk.GetSpan();
            if (currentSpan != null)
            {
                return currentSpan.StartChild(name, description ?? string.Empty);
            }

            var currentTransaction = SentrySdk.GetTransaction();
            if (currentTransaction != null)
            {
                return currentTransaction.StartChild(name, description ?? string.Empty);
            }

            return SentrySdk.StartSpan(name, description ?? string.Empty);
        }

        public ISpan StartSpan(string name, string? description = null)
        {
            return new SentrySpan(CreateSentrySpan(name, description));
        }

        public ICacheGetSpan StartCacheGetSpan()
        {
            return new SentrySpan(CreateSentrySpan("cache.get"));
        }

        public ICachePutSpan StartCachePutSpan()
        {
            return new SentrySpan(CreateSentrySpan("cache.put"));
        }
    }
}
