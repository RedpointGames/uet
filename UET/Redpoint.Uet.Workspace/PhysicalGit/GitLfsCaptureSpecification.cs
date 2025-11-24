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

        public bool InterceptStandardError => _proxied.InterceptStandardError;

        private static bool ShouldRetry(string data)
        {
            if (data.Contains($"batch response: Fatal error: Server error:", StringComparison.Ordinal))
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

            _proxied.OnReceiveStandardError(data);
        }

        public void OnReceiveStandardOutput(string data)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (ShouldRetry(data))
            {
                NeedsRetry = true;
            }

            _proxied.OnReceiveStandardOutput(data);
        }

        public string? OnRequestStandardInputAtStartup()
        {
            return _proxied.OnRequestStandardInputAtStartup();
        }
    }
}
