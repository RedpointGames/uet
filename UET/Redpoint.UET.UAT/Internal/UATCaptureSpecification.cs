namespace Redpoint.UET.UAT.Internal
{
    using Redpoint.ProcessExecution;
    using System;

    internal class UATCaptureSpecification : ICaptureSpecification
    {
        private readonly ICaptureSpecification _baseCaptureSpecification;

        public UATCaptureSpecification(ICaptureSpecification baseCaptureSpecification)
        {
            _baseCaptureSpecification = baseCaptureSpecification;
        }

        public bool NeedsRetry { get; private set; } = false;

        public bool ForceRetry { get; private set; } = false;

        public bool InterceptStandardInput => true;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => true;

        private void CheckDataForRetry(string data)
        {
            if (data.Contains("error C3859"))
            {
                // Temporary "PCH out of memory" error that we get from MSVC.
                NeedsRetry = true;
            }
            if (data.Contains("error LNK1107"))
            {
                // Seems to happen sometimes when using clang-tidy?
                NeedsRetry = true;
            }
            if (data.Contains("LLVM ERROR: out of memory"))
            {
                NeedsRetry = true;
            }
            if (data.Contains("had to patch your engine"))
            {
                ForceRetry = true;
            }
        }

        public void OnReceiveStandardError(string data)
        {
            CheckDataForRetry(data);

            if (_baseCaptureSpecification.InterceptStandardError)
            {
                _baseCaptureSpecification.OnReceiveStandardError(data);
            }
            else
            {
                Console.WriteLine(data);
            }
        }

        public void OnReceiveStandardOutput(string data)
        {
            CheckDataForRetry(data);

            if (_baseCaptureSpecification.InterceptStandardOutput)
            {
                _baseCaptureSpecification.OnReceiveStandardOutput(data);
            }
            else
            {
                Console.WriteLine(data);
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            if (_baseCaptureSpecification.InterceptStandardInput)
            {
                return _baseCaptureSpecification.OnRequestStandardInputAtStartup();
            }

            return null;
        }
    }
}
