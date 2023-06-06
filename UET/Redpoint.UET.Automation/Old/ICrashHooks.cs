#if FALSE

namespace Redpoint.UET.Automation.Old
{
    public interface ICrashHooks
    {
        void OnWorkerCrashing(Worker worker);

        bool OnWorkerCrashed(Worker worker, string[] crashLogs);
    }
}

#endif