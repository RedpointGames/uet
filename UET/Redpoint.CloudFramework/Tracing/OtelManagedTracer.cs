namespace Redpoint.CloudFramework.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class OtelManagedTracer : IManagedTracer
    {
        static ActivitySource _source = new ActivitySource("Redpoint.CloudFramework.OtelManagedTracer");

        private class OtelSpan : ISpan
        {
            private Activity? _activity;

            public OtelSpan(Activity? activity)
            {
                _activity = activity;
            }

            public void Dispose()
            {
                _activity?.Stop();
            }

            public void SetExtra(string key, object? value)
            {
                _activity?.SetCustomProperty(key, value);
            }

            public void SetTag(string key, string value)
            {
                _activity?.SetTag(key, value);
            }
        }

        public ISpan StartSpan(string name, string? description = null)
        {
            return new OtelSpan(_source.StartActivity(name));
        }
    }
}
