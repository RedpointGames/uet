namespace Redpoint.CloudFramework.Tracing
{
    public class NullManagedTracer : IManagedTracer
    {
        public ISpan StartSpan(string name, string? description)
        {
            return NullSpan._instance;
        }
    }
}
