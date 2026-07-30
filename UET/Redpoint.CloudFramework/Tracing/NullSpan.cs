namespace Redpoint.CloudFramework.Tracing
{
    internal class NullSpan : ICacheGetSpan, ICachePutSpan
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

        public bool Hit { get; set; }

        public bool Write { get; set; }

        public string Key
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
