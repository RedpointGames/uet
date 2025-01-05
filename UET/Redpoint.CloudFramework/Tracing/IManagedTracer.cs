namespace Redpoint.CloudFramework.Tracing
{
    public interface IManagedTracer
    {
        ISpan StartSpan(string name, string? description = null);
    }
}
