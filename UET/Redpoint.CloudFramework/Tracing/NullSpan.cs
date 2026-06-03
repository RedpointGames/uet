namespace Redpoint.CloudFramework.Tracing
{
    internal class NullSpan : ISpan
    {
        internal static NullSpan _instance = new NullSpan();

        private NullSpan()
        {
        }

        public string DisplayName
        {
            get => string.Empty;
            set
            {
            }
        }

        public void SetTag(string key, string value)
        {
        }

        public void Dispose()
        {
        }
    }
}
