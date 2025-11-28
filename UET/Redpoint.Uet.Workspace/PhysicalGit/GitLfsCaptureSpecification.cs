namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class GitLfsCaptureSpecification : ICaptureSpecification
    {
        private readonly ICaptureSpecification _proxied;

        public GitLfsCaptureSpecification(ICaptureSpecification proxied)
        {
            _proxied = proxied;
        }

        public bool NeedsRetry { get; set; }

        public bool InterceptStandardInput => _proxied.InterceptStandardInput;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => true;

        private static bool ShouldRetry(string data)
        {
            if (data.Contains($"batch response: Fatal error: Server error:", StringComparison.Ordinal) ||
                data.Contains($"error: failed to fetch some objects from", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        public void OnReceiveStandardError(string data)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (ShouldRetry(data))
            {
                NeedsRetry = true;
            }

            if (_proxied.InterceptStandardError)
            {
                _proxied.OnReceiveStandardError(data);
            }
            else
            {
                Console.Error.WriteLine(data.TrimEnd('\n'));
            }
        }

        public void OnReceiveStandardOutput(string data)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (ShouldRetry(data))
            {
                NeedsRetry = true;
            }

            if (_proxied.InterceptStandardOutput)
            {
                _proxied.OnReceiveStandardOutput(data);
            }
            else
            {
                Console.WriteLine(data.TrimEnd('\n'));
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            return _proxied.OnRequestStandardInputAtStartup();
        }
    }
}
