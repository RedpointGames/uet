namespace Redpoint.ProcessExecution
{
    internal class SilenceCaptureSpecification : ICaptureSpecification
    {
        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => true;

        public bool InterceptStandardInput => false;

        public void OnReceiveStandardError(string data)
        {
        }

        public void OnReceiveStandardOutput(string data)
        {
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }
    }
}
