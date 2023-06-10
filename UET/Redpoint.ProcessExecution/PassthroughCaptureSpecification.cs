namespace Redpoint.ProcessExecution
{
    internal class PassthroughCaptureSpecification : ICaptureSpecification
    {
        public bool InterceptStandardOutput => false;

        public bool InterceptStandardError => false;

        public bool InterceptStandardInput => false;

        public void OnReceiveStandardError(string data)
        {
            throw new NotSupportedException();
        }

        public void OnReceiveStandardOutput(string data)
        {
            throw new NotSupportedException();
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }
    }
}
