namespace AutomationRunner
{
    public interface ICrashHooks
    {
        void OnWorkerCrashing(Worker worker);

        bool OnWorkerCrashed(Worker worker, string[] crashLogs);
    }
}
