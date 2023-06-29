namespace Redpoint.Uet.Automation.Worker
{
    public delegate Task OnWorkerExited(IWorker worker, int exitCode, IWorkerCrashData? crashData);
}
