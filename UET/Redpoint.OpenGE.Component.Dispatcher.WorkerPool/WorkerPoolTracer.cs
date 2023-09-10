namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    public abstract class WorkerPoolTracer
    {
        [Conditional("DEBUG")]
        public abstract void AddTracingMessage(string message, [CallerMemberName] string memberName = "");
    }
}
