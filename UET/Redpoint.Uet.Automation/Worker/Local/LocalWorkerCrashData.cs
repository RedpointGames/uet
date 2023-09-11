namespace Redpoint.Uet.Automation.Worker.Local
{
    internal sealed class LocalWorkerCrashData : IWorkerCrashData
    {
        private readonly string _callstack;

        public LocalWorkerCrashData(string callstack)
        {
            _callstack = callstack;
        }

        public string CrashErrorMessage => _callstack;
    }
}
