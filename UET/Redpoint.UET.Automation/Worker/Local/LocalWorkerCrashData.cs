namespace Redpoint.UET.Automation.Worker.Local
{
    internal class LocalWorkerCrashData : IWorkerCrashData
    {
        private readonly string _callstack;

        public LocalWorkerCrashData(string callstack)
        {
            _callstack = callstack;
        }

        public string CrashErrorMessage => _callstack;
    }
}
