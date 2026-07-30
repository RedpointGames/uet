namespace Redpoint.CloudFramework.Tracing
{
    public class NullManagedTracer : IManagedTracer
    {
        public ICacheGetSpan StartCacheGetSpan()
        {
            return NullSpan._instance;
        }

        public ICachePutSpan StartCachePutSpan()
        {
            return NullSpan._instance;
        }

        public ISpan StartSpan(string name, string? description)
        {
            return NullSpan._instance;
        }
    }
}
