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
        internal static string _sourceName = "Redpoint.CloudFramework.OtelManagedTracer";

        static ActivitySource _source = new ActivitySource(_sourceName);

        private class OtelSpan : ISpan
        {
            private Activity? _activity;

            public OtelSpan(Activity? activity)
            {
                _activity = activity;
            }

            public string DisplayName
            {
                get { return _activity?.DisplayName ?? string.Empty; }
                set { _activity?.DisplayName = value; }
            }

            public void Dispose()
            {
                _activity?.Stop();
            }

            public void SetTag(string key, string value)
            {
                _activity?.SetTag(key, value);
            }
        }

        public ISpan StartSpan(string name, string? description = null)
        {
            var activity = _source.StartActivity(name);
            activity?.DisplayName = description ?? string.Empty;
            return new OtelSpan(activity);
        }
    }
}
