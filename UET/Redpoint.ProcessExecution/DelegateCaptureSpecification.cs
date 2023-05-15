namespace Redpoint.ProcessExecution
{
    internal class DelegateCaptureSpecification : ICaptureSpecification
    {
        private readonly CaptureSpecificationDelegates _captureSpecification;

        public DelegateCaptureSpecification(CaptureSpecificationDelegates delegates)
        {
            _captureSpecification = delegates;
        }

        public bool InterceptStandardInput => true;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => _captureSpecification.ReceiveStderr != null;

        public void OnReceiveStandardOutput(string data)
        {
            if (_captureSpecification.ReceiveStdout(data))
            {
                // Passthrough if we weren't interested in it.
                Console.WriteLine(data);
            }
        }

        public void OnReceiveStandardError(string data)
        {
            if (_captureSpecification.ReceiveStderr != null)
            {
                if (_captureSpecification.ReceiveStderr(data))
                {
                    // Passthrough if we weren't interested in it.
                    Console.WriteLine(data);
                }
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            return null;
        }
    }
}
