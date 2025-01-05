namespace Redpoint.CloudFramework.Tracing
{
    internal class NullSpan : ISpan
    {
        internal static NullSpan _instance = new NullSpan();

        private NullSpan()
        {
        }

        public void Dispose()
        {
        }

        public void SetExtra(string key, object? value)
        {
        }

        public void SetTag(string key, string value)
        {
        }
    }
}
